/*
SimpleAPIGateway.java
Carlos Pineda G. 2024, 2025.
*/
import java.io.*; import java.net.*; import javax.net.ssl.*; import java.io.ByteArrayOutputStream;

class SimpleAPIGateway {
  static String[][] tabla_enrutamiento = {
    {"/api/login","t9-2022630278-gu-svc","80"},
    {"/api/alta_usuario","t9-2022630278-gu-svc","80"},
    {"/api/consulta_usuario","t9-2022630278-gu-svc","80"},
    {"/api/modifica_usuario","t9-2022630278-gu-svc","80"},
    {"/api/borra_usuario","t9-2022630278-gu-svc","80"},
    {"/api/verifica_acceso","t9-2022630278-gu-svc","80"},
    {"/api/alta_articulo","t9-2022630278-ga-svc","80"},
    {"/api/consulta_articulos","t9-2022630278-ga-svc","80"},
    {"/api/compra_articulo","t9-2022630278-gc-svc","80"},
    {"/api/consulta_carrito","t9-2022630278-gc-svc","80"},
    {"/api/elimina_articulo_carrito_compra","t9-2022630278-gc-svc","80"},
    {"/api/elimina_carrito_compra","t9-2022630278-gc-svc","80"},
    {"/api/modifica_carrito_compra","t9-2022630278-gc-svc","80"},
    {"/api/finaliza_compra","t9-2022630278-gc-svc","80"},
    {"/api/Get","t9-2022630278-sw-svc","80"}
  };

  static int TIMEOUT_READ = 60000; // 60s
  static Object obj = new Object();

  static class Worker_1 extends Thread {
    Socket cliente_1,cliente_2;
    Worker_1(Socket c1){ this.cliente_1 = c1; }
    public void run() {
      try {
        InputStream in = cliente_1.getInputStream();
        ByteArrayOutputStream buffer = new ByteArrayOutputStream();
        int previo=-1, actual;
        while (true) {
          actual = in.read();
          if (actual == -1) return;
          buffer.write(actual);
          if (previo=='\r' && actual=='\n') {
            byte[] h = buffer.toByteArray(); int len = h.length;
            if (len>=4 && h[len-4]=='\r' && h[len-3]=='\n' && h[len-2]=='\r' && h[len-1]=='\n') break;
          }
          previo = actual;
        }
        String[] lineas = buffer.toString("ISO-8859-1").split("\r\n");
        String[] peticion = lineas[0].split(" ");
        String metodo = peticion[0];
        String funcion = peticion[1].split("\\?")[0];
        if (!metodo.equals("GET") && !metodo.equals("POST") && !metodo.equals("PUT") && !metodo.equals("DELETE"))
          throw new Exception("Método no soportado");

        int longitud = 0;
        for (String linea : lineas)
          if (linea.toLowerCase().startsWith("content-length:")) {
            longitud = Integer.parseInt(linea.split(":")[1].trim()); break;
          }

        String host=null; int puerto=0;
        for (String[] r : tabla_enrutamiento)
          if (funcion.equals(r[0])) { host=r[1]; puerto=Integer.parseInt(r[2]); break; }
        if (host == null) throw new Exception("Ruta no encontrada");

        System.out.println("Proxy: " + metodo + " " + funcion + " -> " + host + ":" + puerto);

        cliente_2 = new Socket(host, puerto);
        new Worker_2(cliente_1, cliente_2).start();

        OutputStream out = cliente_2.getOutputStream();
        out.write(buffer.toByteArray()); out.flush();

        if (longitud > 0) {
          byte[] body = new byte[4096];
          int restante = longitud;
          while (restante > 0) {
            int n = in.read(body,0,Math.min(body.length,restante));
            if (n == -1) break;
            out.write(body,0,n); out.flush();
            restante -= n;
          }
        }
        synchronized(obj){ obj.wait(); }
      } catch (Exception e) {
        System.err.println("Gateway error: " + e.getMessage());
      } finally {
        try { cliente_1.close(); } catch(Exception ignored){}
      }
    }
  }

  static class Worker_2 extends Thread {
    Socket cliente_1,cliente_2;
    Worker_2(Socket c1,Socket c2){ this.cliente_1=c1; this.cliente_2=c2; }
    public void run() {
      try {
        // NO setSoTimeout: evita cortar respuesta antes de tiempo
        InputStream entrada_2 = cliente_2.getInputStream();
        OutputStream salida_1 = cliente_1.getOutputStream();
        byte[] buffer = new byte[8192]; int n;
        while((n = entrada_2.read(buffer)) != -1) {
          salida_1.write(buffer,0,n); salida_1.flush();
        }
      } catch (IOException e) {
        System.err.println("Proxy IO: " + e.getMessage());
      } finally {
        try { cliente_2.close(); synchronized(obj){ obj.notify(); } } catch (IOException e2) { e2.printStackTrace(); }
      }
    }
  }

  public static void main(String[] args) throws Exception {
    String keystore = System.getenv("keystore");
    String password = System.getenv("password");
    if (keystore == null || password == null) { System.err.println("Faltan variables"); System.exit(1); }
    System.setProperty("javax.net.ssl.keyStore",keystore);
    System.setProperty("javax.net.ssl.keyStorePassword",password);
    SSLServerSocketFactory sf = (SSLServerSocketFactory)SSLServerSocketFactory.getDefault();
    ServerSocket ss = sf.createServerSocket(443);
    for(;;){ Socket c1 = ss.accept(); new Worker_1(c1).start(); }
  }
}