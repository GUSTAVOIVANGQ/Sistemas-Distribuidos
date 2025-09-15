/*
  AdministradorTrafico.java
  Proxy inverso "Administrador de tráfico" que:
  - Recibe una petición GET del navegador.
  - Reenvía la MISMA petición a dos servidores backend (Servidor-1 y Servidor-2).
  - Devuelve al navegador ÚNICAMENTE la respuesta de Servidor-1.
  - Consume y descarta la respuesta de Servidor-2.
  Parámetros:
    1) puerto que escucha el proxy
    2) IP Servidor-1
    3) puerto Servidor-1
    4) IP Servidor-2
    5) puerto Servidor-2
  Uso:
    java AdministradorTrafico <puerto-proxy> <ip1> <puerto1> <ip2> <puerto2>
*/

import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.util.*;

public class AdministradorTrafico {

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

        // Forzar Connection: close para simplificar
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

        // Conectar a backends
        s1 = new Socket();
        s2 = new Socket();
        s1.connect(new InetSocketAddress(ip1, port1), 10000);
        s2.connect(new InetSocketAddress(ip2, port2), 10000);
        s1.setSoTimeout(30000);
        s2.setSoTimeout(30000);

        // Enviar misma solicitud a ambos
        s1.getOutputStream().write(requestBytes);
        s1.getOutputStream().flush();
        s2.getOutputStream().write(requestBytes);
        s2.getOutputStream().flush();

  // Respuestas: S1 → cliente, S2 → drenar
  final Socket s1Final = s1;
  final Socket s2Final = s2;
  final Socket clienteFinal = cliente;
  Thread resp1 = new Thread(() -> pipeBytes("S1->CLIENTE", safeIn(s1Final), safeOut(clienteFinal)));
  Thread resp2 = new Thread(() -> drainBytes("S2->DRAIN", safeIn(s2Final)));

  resp1.start();
  resp2.start();

  resp1.join(); // esperamos la respuesta principal
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
      try {
        out.write(line.getBytes(StandardCharsets.ISO_8859_1));
        out.write('\r'); out.write('\n');
      } catch (IOException e) {
        throw new RuntimeException(e);
      }
    }
    private static InputStream safeIn(Socket s) { try { return s.getInputStream(); } catch (IOException e) { throw new RuntimeException(e); } }
    private static OutputStream safeOut(Socket s) { try { return s.getOutputStream(); } catch (IOException e) { throw new RuntimeException(e); } }

    private static void pipeBytes(String tag, InputStream in, OutputStream out) {
      byte[] buf = new byte[8192];
      int n;
      try {
        while ((n = in.read(buf)) != -1) {
          out.write(buf, 0, n);
          out.flush();
        }
      } catch (IOException e) {
      } finally {
        try { out.flush(); } catch (Exception ignore) {}
      }
    }
    private static void drainBytes(String tag, InputStream in) {
      byte[] buf = new byte[8192];
      try { while (in.read(buf) != -1) {} } catch (IOException e) {}
    }

    private static void sendSimpleResponse(OutputStream out, String status, String bodyText) {
      try {
        byte[] body = ("<html><body><h1>" + status + "</h1><p>" + escapeHtml(bodyText) + "</p></body></html>").getBytes(StandardCharsets.UTF_8);
        String headers =
          "HTTP/1.1 " + status + "\r\n" +
          "Content-Type: text/html; charset=utf-8\r\n" +
          "Content-Length: " + body.length + "\r\n" +
          "Connection: close\r\n" +
          "\r\n";
        out.write(headers.getBytes(StandardCharsets.ISO_8859_1));
        out.write(body);
        out.flush();
      } catch (IOException ignore) {}
    }
    private static String escapeHtml(String s) { return s.replace("&","&amp;").replace("<","&lt;").replace(">","&gt;"); }
    private static void closeQuietly(Socket s) { if (s!=null) try { s.close(); } catch (IOException ignore) {} }
  }

  public static void main(String[] args) throws Exception {
    if (args.length != 5) {
      System.err.println("Uso:\njava AdministradorTrafico <puerto-proxy> <ip1> <puerto1> <ip2> <puerto2>");
      System.exit(1);
    }
    int puertoProxy = Integer.parseInt(args[0]);
    String ip1 = args[1];
    int port1 = Integer.parseInt(args[2]);
    String ip2 = args[3];
    int port2 = Integer.parseInt(args[4]);

    ServerSocket ss = new ServerSocket(puertoProxy);
    System.out.println("AdministradorTrafico escuchando en puerto " + puertoProxy +
                       " -> Backend1 " + ip1 + ":" + port1 +
                       " | Backend2 " + ip2 + ":" + port2);

    for (;;) {
      Socket cliente = ss.accept();
      new ProxyWorker(cliente, ip1, port1, ip2, port2).start();
    }
  }
}