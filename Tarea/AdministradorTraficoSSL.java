/*
  AdministradorTraficoSSL.java
  Servidor SSL que recibe peticiones HTTPS y las reenvía por HTTP a:
    - Servidor 1 (produce la respuesta que se devuelve al cliente)
    - Servidor 2 (solo recibe la misma escritura para replicación)
  Keystore embebido por requisitos de la práctica.

  Ejecutar (ejemplo):
    sudo java AdministradorTraficoSSL 127.0.0.1 8080 127.0.0.1 8081 443
*/

import javax.net.ssl.SSLContext;
import javax.net.ssl.SSLServerSocket;
import javax.net.ssl.SSLServerSocketFactory;
import java.io.*;
import java.net.Socket;
import java.security.KeyStore;
import javax.net.ssl.KeyManagerFactory;

public class AdministradorTraficoSSL {

  // Ajusta a la ruta y contraseña que generaste con keytool en la VM principal
  private static final String KEYSTORE_PATH = "/home/azureuser/trafico.keystore";
  private static final String KEYSTORE_PASS = "Xxxxxxx0";

  static String host_remoto_1;
  static int puerto_remoto_1;
  static String host_remoto_2;
  static int puerto_remoto_2;
  static int puerto_local_https;

  static class Worker_1 extends Thread {
    Socket clienteTLS;
    Socket servidor_1, servidor_2;

    Worker_1(Socket clienteTLS) {
      this.clienteTLS = clienteTLS;
    }

    public void run() {
      try {
        // Conexiones HTTP a los backends
        servidor_1 = new Socket(host_remoto_1, puerto_remoto_1);
        servidor_2 = new Socket(host_remoto_2, puerto_remoto_2);

        // Hilo que copia la respuesta del servidor_1 hacia el cliente TLS
        new Worker_2(clienteTLS, servidor_1).start();

        InputStream entrada_cliente = clienteTLS.getInputStream();
        OutputStream salida_srv1 = servidor_1.getOutputStream();
        OutputStream salida_srv2 = servidor_2.getOutputStream();

        byte[] buffer = new byte[4096];
        int n;
        while ((n = entrada_cliente.read(buffer)) != -1) {
          // Envía al servidor principal
          salida_srv1.write(buffer, 0, n);
          salida_srv1.flush();
          // Replica al servidor secundario (no esperamos su respuesta)
          salida_srv2.write(buffer, 0, n);
          salida_srv2.flush();
        }
      } catch (IOException e) {
        // Silencioso para la práctica
      } finally {
        try {
          if (clienteTLS != null) clienteTLS.close();
          if (servidor_1 != null) servidor_1.close();
          if (servidor_2 != null) servidor_2.close();
        } catch (IOException ex) {
          ex.printStackTrace();
        }
      }
    }
  }

  static class Worker_2 extends Thread {
    Socket clienteTLS, servidor_1;

    Worker_2(Socket clienteTLS, Socket servidor_1) {
      this.clienteTLS = clienteTLS;
      this.servidor_1 = servidor_1;
    }

    public void run() {
      try {
        InputStream entrada_srv1 = servidor_1.getInputStream();
        OutputStream salida_cliente = clienteTLS.getOutputStream();
        byte[] buffer = new byte[8192];
        int n;
        while ((n = entrada_srv1.read(buffer)) != -1) {
          salida_cliente.write(buffer, 0, n);
          salida_cliente.flush();
        }
      } catch (IOException e) {
        // Silencioso para la práctica
      } finally {
        try {
          if (clienteTLS != null) clienteTLS.close();
          if (servidor_1 != null) servidor_1.close();
        } catch (IOException ex) {
          ex.printStackTrace();
        }
      }
    }
  }

  private static SSLServerSocket buildSSLServer(int port) throws Exception {
    KeyStore ks = KeyStore.getInstance("JKS");
    try (FileInputStream fis = new FileInputStream(KEYSTORE_PATH)) {
      ks.load(fis, KEYSTORE_PASS.toCharArray());
    }

    KeyManagerFactory kmf = KeyManagerFactory.getInstance(KeyManagerFactory.getDefaultAlgorithm());
    kmf.init(ks, KEYSTORE_PASS.toCharArray());

    SSLContext sc = SSLContext.getInstance("TLS");
    sc.init(kmf.getKeyManagers(), null, null);

    SSLServerSocketFactory ssf = sc.getServerSocketFactory();
    SSLServerSocket sss = (SSLServerSocket) ssf.createServerSocket(port);
    // Protocolos razonables en Java 8
    sss.setEnabledProtocols(new String[] {"TLSv1.2"});
    return sss;
  }

  public static void main(String[] args) throws Exception {
    if (args.length != 5) {
      System.err.println("Uso:");
      System.err.println("  java AdministradorTraficoSSL <host-remoto-1> <puerto-remoto-1> <host-remoto-2> <puerto-remoto-2> <puerto-local-https>");
      System.exit(1);
    }

    host_remoto_1 = args[0];
    puerto_remoto_1 = Integer.parseInt(args[1]);
    host_remoto_2 = args[2];
    puerto_remoto_2 = Integer.parseInt(args[3]);
    puerto_local_https = Integer.parseInt(args[4]);

    System.out.println("HTTPS escuchando en " + puerto_local_https +
        " -> HTTP " + host_remoto_1 + ":" + puerto_remoto_1 +
        " + replica " + host_remoto_2 + ":" + puerto_remoto_2);
    System.out.println("Usando keystore: " + KEYSTORE_PATH);

    SSLServerSocket sss = buildSSLServer(puerto_local_https);

    for (;;) {
      // Acepta conexiones TLS (desde Internet)
      Socket clienteTLS = sss.accept();
      new Worker_1(clienteTLS).start();
    }
  }
}