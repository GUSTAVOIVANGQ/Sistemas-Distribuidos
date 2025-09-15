/*
  AdministradorTraficoSSL.java
  Proxy inverso con sockets seguros (SSL/TLS) en el lado del servidor.
  - Termina TLS en este proceso y reenvía HTTP plano a los dos backends.
  - Devuelve al cliente SOLO la respuesta de Servidor-1; la de Servidor-2 se descarta.
  Parámetros admitidos:
    - Modo 7 args: <puerto-ssl> <keystore> <password> <ip1> <puerto1> <ip2> <puerto2>
    - Modo 6 args: <keystore> <password> <ip1> <puerto1> <ip2> <puerto2>  (puerto por defecto: 8443)
  Ejemplos:
    java AdministradorTraficoSSL 443 keystore_servidor.jks changeit 10.0.0.5 8080 10.0.0.6 8080
    java AdministradorTraficoSSL keystore_servidor.jks changeit 10.0.0.5 8080 10.0.0.6 8080  (escucha 8443)
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
    private final String ip1; private final int port1;
    private final String ip2; private final int port2;

    ProxyWorker(Socket cliente, String ip1, int port1, String ip2, int port2) {
      this.cliente = cliente; this.ip1 = ip1; this.port1 = port1; this.ip2 = ip2; this.port2 = port2;
    }

    @Override public void run() {
      try {
        cliente.setSoTimeout(30000);
        BufferedReader cbr = new BufferedReader(new InputStreamReader(cliente.getInputStream(), StandardCharsets.ISO_8859_1));
        OutputStream cout = cliente.getOutputStream();

        String requestLine = cbr.readLine();
        if (requestLine == null || requestLine.isEmpty()) {
          sendSimpleResponse(cout, "400 Bad Request", "Solicitud vacía o inválida"); return;
        }

        List<String> headers = new ArrayList<>();
        String line;
        while ((line = cbr.readLine()) != null) { if (line.isEmpty()) break; headers.add(line); }

        boolean hasConn = false;
        for (int i = 0; i < headers.size(); i++) {
          String h = headers.get(i); int idx = h.indexOf(':');
          if (idx > 0 && h.substring(0, idx).trim().equalsIgnoreCase("Connection")) {
            headers.set(i, "Connection: close"); hasConn = true;
          }
        }
        if (!hasConn) headers.add("Connection: close");

        ByteArrayOutputStream reqBuf = new ByteArrayOutputStream();
        writeCRLFLine(reqBuf, requestLine);
        for (String h : headers) writeCRLFLine(reqBuf, h);
        reqBuf.write("\r\n".getBytes(StandardCharsets.ISO_8859_1));
        byte[] requestBytes = reqBuf.toByteArray();

        final Socket s1 = new Socket();
        final Socket s2 = new Socket();
        s1.connect(new InetSocketAddress(ip1, port1), 10000);
        s2.connect(new InetSocketAddress(ip2, port2), 10000);
        s1.setSoTimeout(30000);
        s2.setSoTimeout(30000);

        s1.getOutputStream().write(requestBytes); s1.getOutputStream().flush();
        s2.getOutputStream().write(requestBytes); s2.getOutputStream().flush();

        Thread t1 = new Thread(() -> pipeBytes("S1->CLIENTE", safeIn(s1), safeOut(cliente)));
        Thread t2 = new Thread(() -> drainBytes("S2->DRAIN", safeIn(s2)));
        t1.start(); t2.start();
        t1.join();
        try { cliente.shutdownOutput(); } catch (Exception ignore) {}
        t2.join();

        // Cierre manual de los sockets y del cliente
        try { s1.close(); } catch (IOException ignore) {}
        try { s2.close(); } catch (IOException ignore) {}
        try { cliente.close(); } catch (IOException ignore) {}

      } catch (SocketTimeoutException ste) {
        try { sendSimpleResponse(cliente.getOutputStream(), "504 Gateway Timeout", "El backend no respondió a tiempo"); } catch (Exception ignore) {}
        try { cliente.close(); } catch (IOException ignore) {}
      } catch (ConnectException ce) {
        try { sendSimpleResponse(cliente.getOutputStream(), "502 Bad Gateway", "No se pudo conectar a alguno de los backends"); } catch (Exception ignore) {}
        try { cliente.close(); } catch (IOException ignore) {}
      } catch (Exception e) {
        try { sendSimpleResponse(cliente.getOutputStream(), "500 Internal Server Error", "Error en el proxy inverso"); } catch (Exception ignore) {}
        try { cliente.close(); } catch (IOException ignore) {}
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
        String headers = "HTTP/1.1 " + status + "\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + body.length + "\r\nConnection: close\r\n\r\n";
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
    try (InputStream is = new FileInputStream(keystorePath)) { ks.load(is, password.toCharArray()); }
    KeyManagerFactory kmf = KeyManagerFactory.getInstance(KeyManagerFactory.getDefaultAlgorithm());
    kmf.init(ks, password.toCharArray());
    SSLContext ctx = SSLContext.getInstance("TLS");
    ctx.init(kmf.getKeyManagers(), null, null);
    SSLServerSocketFactory ssf = ctx.getServerSocketFactory();
    SSLServerSocket server = (SSLServerSocket) ssf.createServerSocket(port);
    // Opcional: server.setEnabledProtocols(new String[]{"TLSv1.2","TLSv1.3"});
    return server;
  }

  public static void main(String[] args) throws Exception {
    final int DEFAULT_SSL_PORT = 8443;
    int sslPort;
    String keystorePath, keystorePass, ip1, ip2;
    int port1, port2;

    if (args.length == 7) {
      sslPort = Integer.parseInt(args[0]);
      keystorePath = args[1]; keystorePass = args[2];
      ip1 = args[3]; port1 = Integer.parseInt(args[4]);
      ip2 = args[5]; port2 = Integer.parseInt(args[6]);
    } else if (args.length == 6) {
      sslPort = DEFAULT_SSL_PORT;
      keystorePath = args[0]; keystorePass = args[1];
      ip1 = args[2]; port1 = Integer.parseInt(args[3]);
      ip2 = args[4]; port2 = Integer.parseInt(args[5]);
    } else {
      System.err.println("Uso:\n  java AdministradorTraficoSSL <puerto-ssl> <keystore> <password> <ip1> <puerto1> <ip2> <puerto2>\n" +
                         "  o bien (puerto por defecto 8443):\n  java AdministradorTraficoSSL <keystore> <password> <ip1> <puerto1> <ip2> <puerto2>");
      System.exit(1); return;
    }

    SSLServerSocket ss = createSSLServerSocket(sslPort, keystorePath, keystorePass);
    System.out.println("AdministradorTraficoSSL escuchando en " + sslPort + " con keystore " + keystorePath +
                       " -> Backend1 " + ip1 + ":" + port1 + " | Backend2 " + ip2 + ":" + port2);

    for (;;) {
      SSLSocket cliente = (SSLSocket) ss.accept();
      try { cliente.startHandshake(); } catch (IOException e) { cliente.close(); continue; }
      new ProxyWorker(cliente, ip1, port1, ip2, port2).start();
    }
  }
}