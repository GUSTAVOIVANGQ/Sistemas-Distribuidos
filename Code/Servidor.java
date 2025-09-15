import java.io.*;
import java.net.*;
import java.nio.ByteBuffer;

public class Servidor {

    // Método auxiliar para leer un número específico de bytes
    static void read(DataInputStream f, byte[] b, int posicion, int longitud) throws Exception {
        while (longitud > 0) {
            int n = f.read(b, posicion, longitud);
            posicion += n;
            longitud -= n;
        }
    }

    public static void main(String[] args) throws Exception {
        // Creamos un ServerSocket que va a abrir el puerto 50000
        ServerSocket servidor = new ServerSocket(50000);

        System.out.println("Servidor esperando conexión en el puerto 50000...");

        // El método accept() es bloqueante, espera una conexión del cliente
        Socket conexion = servidor.accept();

        System.out.println("Conexión establecida con el cliente.");

        // Creamos un stream de salida y un stream de entrada
        DataOutputStream salida = new DataOutputStream(conexion.getOutputStream());
        DataInputStream entrada = new DataInputStream(conexion.getInputStream());

        // El servidor recibe un entero de 32 bits y lo imprime
        int n = entrada.readInt();
        System.out.println("Entero recibido: " + n);

        // El servidor recibe un número punto flotante de 64 bits y lo imprime
        double x = entrada.readDouble();
        System.out.println("Double recibido: " + x);

        // El servidor recibe una cadena de cuatro caracteres
        byte[] buffer = new byte[4];
        read(entrada, buffer, 0, 4);
        System.out.println("Cadena recibida: " + new String(buffer, "UTF-8"));

        // El servidor envía una cadena de cuatro caracteres
        salida.write("HOLA".getBytes());
        System.out.println("Cadena 'HOLA' enviada.");

        // El servidor recibe cinco números punto flotante empacados en un arreglo de bytes
        byte[] a = new byte[5 * 8];
        read(entrada, a, 0, 5 * 8);

        // Convertimos el arreglo de bytes a un objeto ByteBuffer
        ByteBuffer b = ByteBuffer.wrap(a);

        // Extraemos los números punto flotante y los imprimimos
        System.out.println("Números recibidos usando ByteBuffer:");
        for (int i = 0; i < 5; i++) {
            System.out.println(b.getDouble());
        }

        // Cerramos la conexión con el cliente
        conexion.close();
        servidor.close();

        System.out.println("Conexión cerrada.");
    }
}
