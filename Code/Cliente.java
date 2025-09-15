import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.net.Socket;
import java.nio.ByteBuffer;

public class Cliente {

    static void read(DataInputStream f, byte[] b, int posicion, int longitud) throws Exception {
        while (longitud > 0) {
            int n = f.read(b, posicion, longitud);
            posicion += n;
            longitud -= n;
        }
    }

    public static void main(String[] args) throws Exception {
        // Bloque de reintento para conectar con el servidor.
        Socket conexion = null;
        for (;;) {
            try {
                conexion = new Socket("localhost", 50000);
                break;
            } catch (Exception e) {
                System.out.println("Esperando que el servidor inicie...");
                Thread.sleep(100);
            }
        }
        System.out.println("Conectado al servidor.");

        // Streams para enviar y recibir datos.
        DataOutputStream salida = new DataOutputStream(conexion.getOutputStream());
        DataInputStream entrada = new DataInputStream(conexion.getInputStream());

        // Ejemplo 1: Enviar un entero de 32 bits (123).
        salida.writeInt(123);
        System.out.println("Enviando entero: 123");

        // Ejemplo 2: Enviar un número de punto flotante de 64 bits.
        salida.writeDouble(1234567890.1234567890);
        System.out.println("Enviando double: 1234567890.1234567890");

        // Ejemplo 3: Enviar la cadena "hola" como bytes.
        salida.write("hola".getBytes());
        System.out.println("Enviando string: 'hola'");

        // Ejemplo 4: Recibir una cadena de 4 bytes del servidor y mostrarla.
        byte[] buffer = new byte[4];
        read(entrada, buffer, 0, 4);
        System.out.println("Recibido del servidor: " + new String(buffer, "UTF-8"));

        // Ejemplo 5: Enviar 5 números doubles en un solo paquete con ByteBuffer.
        ByteBuffer b = ByteBuffer.allocate(5 * 8);
        b.putDouble(1.1);
        b.putDouble(1.2);
        b.putDouble(1.3);
        b.putDouble(1.4);
        b.putDouble(1.5);
        byte[] a = b.array();
        salida.write(a);
        System.out.println("Enviando 5 doubles en un paquete.");
        
        // Retardo para asegurar la recepción del servidor y cerrar la conexión.
        Thread.sleep(1000);
        conexion.close();
        System.out.println("Conexión cerrada.");
    }
}