/*
  AdministradorTraficoSSL.java
  Igual que AdministradorTrafico pero con sockets seguros en el lado del servidor (SSL/TLS).
  - El cliente (navegador) se conecta por HTTPS (443 → REDIRECT a 8443).
  - El proxy termina TLS y reenvía por HTTP sin cifrar a los backends (VM2 y VM3).
  - Parámetros:
      1) ruta del keystore JKS (ej: ./keystore_servidor.jks)
      2) password del keystore
      3) IP Servidor-1
      4) puerto Servidor-1
      5) IP Servidor-2
      6) puerto Servidor-2
  - Escucha SIEMPRE en puerto 8443 (como pide la práctica).
  Uso:
    java AdministradorTraficoSSL <keystore> <password> <ip1> <puerto1> <ip2> <puerto2>
*/

import javax.net.ssl.*;
import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.security.*;
import java.security.cert.CertificateException;
import java.util.*;

public class AdministradorTraficoSSL {

  static class ProxyWorker extends Thread {
    private final Socket cliente;
    private final String ip1;
    private final int port1;
    private final String ip2;
    private final int port2;

    ProxyWorker(Socket cliente, String ip1, int port1, String ip2, int port2) {
      this.cliente = cliente;
      this.ip1 = ip1;
      this.port1 = port1;
      this.ip2 = ip2;
      this.port2 = port2;
    }

    @Override
    public void run() {
      Socket s1 = null;
      Socket s2 = null;
      try {
        cliente.setSoTimeout(30000);
        InputStream cin = cliente.getInputStream();
        OutputStream cout = cliente.getOutputStream();
        BufferedReader cbr = new BufferedReader(new InputStreamReader(cin, StandardCharsets.ISO_8859_1));

        String requestLine = cbr.readLine();
        if (requestLine == null || requestLine.isEmpty()) {
          sendSimpleResponse(cout, "400 Bad Request", "Solicitud vacía o inválida");
          return;
        }

        List<String> headerLines = new ArrayList<>();
        String line;
        while ((line = cbr.readLine()) != null) {
          if (line.isEmpty()) break;
          headerLines.add(line);
        }

        boolean hasConnection = false;
        for (int i = 0; i < headerLines.size(); i++) {
          String h = headerLines.get(i);
          int idx = h.indexOf(':');
          if (idx > 0) {
            String name = h.substring(0, idx).trim();
            if (name.equalsIgnoreCase("Connection")) {
              headerLines.set(i, "Connection: close");
              hasConnection = true;
            }
          }
        }
        if (!hasConnection) headerLines.add("Connection: close");

        ByteArrayOutputStream reqBuf = new ByteArrayOutputStream();
        writeCRLFLine(reqBuf, requestLine);
        for (String h : headerLines) writeCRLFLine(reqBuf, h);
        reqBuf.write("\r\n".getBytes(StandardCharsets.ISO_8859_1));
        byte[] requestBytes = reqBuf.toByteArray();

        // Conectar a backends (HTTP plano)
        s1 = new Socket();
        s2 = new Socket();
        s1.connect(new InetSocketAddress(ip1, port1), 10000);
        s2.connect(new InetSocketAddress(ip2, port2), 10000);
        s1.setSoTimeout(30000);
        s2.setSoTimeout(30000);

        s1.getOutputStream().write(requestBytes);
        s1.getOutputStream().flush();
        s2.getOutputStream().write(requestBytes);
        s2.getOutputStream().flush();

        final Socket s1Final = s1;
        final Socket s2Final = s2;
        final Socket clienteFinal = cliente;
        Thread resp1 = new Thread(() -> pipeBytes("S1->CLIENTE", safeIn(s1Final), safeOut(clienteFinal)));
        Thread resp2 = new Thread(() -> drainBytes("S2->DRAIN", safeIn(s2Final)));
        resp1.start(); resp2.start();

        resp1.join();
        try { clienteFinal.shutdownOutput(); } catch (Exception ignore) {}
        resp2.join();

      } catch (SocketTimeoutException ste) {
        try { sendSimpleResponse(cliente.getOutputStream(), "504 Gateway Timeout", "El backend no respondió a tiempo"); } catch (Exception ignore) {}
      } catch (ConnectException ce) {
        try { sendSimpleResponse(cliente.getOutputStream(), "502 Bad Gateway", "No se pudo conectar a alguno de los backends"); } catch (Exception ignore) {}
      } catch (Exception e) {
        try { sendSimpleResponse(cliente.getOutputStream(), "500 Internal Server Error", "Error en el proxy inverso"); } catch (Exception ignore) {}
      } finally {
        closeQuietly(s1);
        closeQuietly(s2);
        closeQuietly(cliente);
      }
    }

    private static void writeCRLFLine(OutputStream out, String line) {
      try { out.write(line.getBytes(StandardCharsets.ISO_8859_1)); out.write('\r'); out.write('\n'); } catch (IOException e) { throw new RuntimeException(e); }
    }
    private static InputStream safeIn(Socket s) { try { return s.getInputStream(); } catch (IOException e) { throw new RuntimeException(e); } }
    private static OutputStream safeOut(Socket s) { try { return s.getOutputStream(); } catch (IOException e) { throw new RuntimeException(e); } }
    private static void pipeBytes(String tag, InputStream in, OutputStream out) {
      byte[] buf = new byte[8192]; int n;
      try { while ((n = in.read(buf)) != -1) { out.write(buf,0,n); out.flush(); } } catch (IOException e) {} finally { try { out.flush(); } catch (Exception ignore) {} }
    }
    private static void drainBytes(String tag, InputStream in) { byte[] buf = new byte[8192]; try { while (in.read(buf) != -1) {} } catch (IOException e) {} }
    private static void sendSimpleResponse(OutputStream out, String status, String bodyText) {
      try {
        byte[] body = ("<html><body><h1>" + status + "</h1><p>" + escapeHtml(bodyText) + "</p></body></html>").getBytes(StandardCharsets.UTF_8);
        String headers = "HTTP/1.1 " + status + "\r\n" +
                         "Content-Type: text/html; charset=utf-8\r\n" +
                         "Content-Length: " + body.length + "\r\n" +
                         "Connection: close\r\n\r\n";
        out.write(headers.getBytes(StandardCharsets.ISO_8859_1)); out.write(body); out.flush();
      } catch (IOException ignore) {}
    }
    private static String escapeHtml(String s) { return s.replace("&","&amp;").replace("<","&lt;").replace(">","&gt;"); }
    private static void closeQuietly(Socket s) { if (s!=null) try { s.close(); } catch (IOException ignore) {} }
  }

  private static SSLServerSocket createSSLServerSocket(int port, String keystorePath, String password)
      throws KeyStoreException, IOException, NoSuchAlgorithmException, CertificateException,
             UnrecoverableKeyException, KeyManagementException {
    KeyStore ks = KeyStore.getInstance("JKS");
    try (InputStream is = new FileInputStream(keystorePath)) {
      ks.load(is, password.toCharArray());
    }
    KeyManagerFactory kmf = KeyManagerFactory.getInstance(KeyManagerFactory.getDefaultAlgorithm());
    kmf.init(ks, password.toCharArray());
    SSLContext ctx = SSLContext.getInstance("TLS");
    ctx.init(kmf.getKeyManagers(), null, null);
    SSLServerSocketFactory ssf = ctx.getServerSocketFactory();
    SSLServerSocket server = (SSLServerSocket) ssf.createServerSocket(port);
    // Opcional: restringir versiones/ciphers
    // server.setEnabledProtocols(new String[]{"TLSv1.2","TLSv1.3"});
    // server.setNeedClientAuth(false);
    return server;
  }

  public static void main(String[] args) throws Exception {
    if (args.length != 6) {
      System.err.println("Uso:\njava AdministradorTraficoSSL <keystore> <password> <ip1> <puerto1> <ip2> <puerto2>");
      System.exit(1);
    }
    String keystorePath = args[0];
    String keystorePass = args[1];
    String ip1 = args[2];
    int port1 = Integer.parseInt(args[3]);
    String ip2 = args[4];
    int port2 = Integer.parseInt(args[5]);

    final int SSL_PORT = 8443;
    SSLServerSocket ss = createSSLServerSocket(SSL_PORT, keystorePath, keystorePass);
    System.out.println("AdministradorTraficoSSL escuchando en 8443 con keystore " + keystorePath +
                       " -> Backend1 " + ip1 + ":" + port1 + " | Backend2 " + ip2 + ":" + port2);

    for (;;) {
      SSLSocket cliente = (SSLSocket) ss.accept();
      // Iniciar handshake cuanto antes para detectar problemas de cert
      try { cliente.startHandshake(); } catch (IOException e) { cliente.close(); continue; }
      new ProxyWorker(cliente, ip1, port1, ip2, port2).start();
    }
  }
}