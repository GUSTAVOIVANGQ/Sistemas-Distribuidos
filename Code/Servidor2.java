import java.net.*;
import java.io.*;
import java.nio.ByteBuffer;

/**
 * Servidor multithread que maneja múltiples conexiones de clientes de forma concurrente.
 * Por cada conexión aceptada, se crea un nuevo hilo (Worker) para procesar la solicitud.
 */
class Servidor2 {

  /**
   * Lee bytes de un flujo de entrada hasta llenar un buffer.
   * Este método es crucial para asegurar que se lean todos los datos esperados,
   * ya que una sola llamada a read() no garantiza leer la cantidad completa.
   *
   * @param f        El DataInputStream del cual leer.
   * @param b        El buffer de bytes a llenar.
   * @param posicion La posición inicial en el buffer.
   * @param longitud El número total de bytes a leer.
   * @throws Exception si ocurre un error de I/O.
   */
  static void read(DataInputStream f, byte[] b, int posicion, int longitud) throws Exception {
    while (longitud > 0) {
      int n = f.read(b, posicion, longitud);
      if (n == -1) {
          throw new EOFException("Se alcanzó el fin del stream inesperadamente.");
      }
      posicion += n;
      longitud -= n;
    }
  }

  /**
   * La clase Worker representa un hilo que atiende a un cliente.
   * Cada instancia de Worker se ejecuta en su propio hilo.
   */
  static class Worker extends Thread {
    Socket conexion;

    /**
     * Constructor que recibe el socket de la conexión con el cliente.
     * @param conexion El socket de la conexión.
     */
    Worker(Socket conexion) {
      this.conexion = conexion;
    }

    /**
     * El método run() contiene la lógica que se ejecutará en el hilo.
     * Procesa los datos recibidos del cliente y envía una respuesta.
     */
    public void run() {
      try {
        // Se crean los flujos de entrada y salida para la comunicación.
        DataOutputStream salida = new DataOutputStream(conexion.getOutputStream());
        DataInputStream entrada = new DataInputStream(conexion.getInputStream());

        // 1. Recibe un entero (int)
        int n = entrada.readInt();
        System.out.println("Entero recibido: " + n);

        // 2. Recibe un doble (double)
        double x = entrada.readDouble();
        System.out.println("Double recibido: " + x);

        // 3. Recibe un buffer de 4 bytes y lo convierte a String
        byte[] buffer = new byte[4];
        read(entrada, buffer, 0, 4);
        System.out.println("String de 4 bytes recibido: " + new String(buffer, "UTF-8"));

        // 4. Envía una cadena de 4 bytes ("HOLA") al cliente
        salida.write("HOLA".getBytes());

        // 5. Recibe un arreglo de 5 doubles (5 * 8 bytes)
        byte[] a = new byte[5 * 8];
        read(entrada, a, 0, 5 * 8);
        ByteBuffer b = ByteBuffer.wrap(a);
        System.out.println("Recibiendo 5 doubles:");
        for (int i = 0; i < 5; i++) {
          System.out.println(b.getDouble());
        }

      } catch (Exception e) {
        System.err.println("Error: " + e.getMessage());
      } finally {
        try {
          // Es fundamental cerrar la conexión para liberar recursos.
          if (conexion != null) {
            conexion.close();
          }
        } catch (Exception e2) {
          System.err.println("Error al cerrar la conexión: " + e2.getMessage());
        }
      }
    }
  }

  /**
   * El método principal del servidor.
   * Escucha conexiones entrantes en un bucle infinito.
   */
  public static void main(String[] args) throws Exception {
    // Se crea un ServerSocket en el puerto 50000.
    ServerSocket servidor = new ServerSocket(50000);
    System.out.println("Servidor iniciado en el puerto 50000. Esperando clientes...");

    // Bucle infinito para aceptar conexiones de clientes.
    for (;;) {
      // accept() es un método bloqueante: espera hasta que un cliente se conecte.
      Socket conexion = servidor.accept();
      System.out.println("Cliente conectado desde: " + conexion.getInetAddress());
      
      // Se crea un nuevo Worker para manejar la conexión del cliente.
      Worker w = new Worker(conexion);

      // Se inicia el hilo del Worker. Esto invoca al método run().
      w.start();
    }
  }
}
