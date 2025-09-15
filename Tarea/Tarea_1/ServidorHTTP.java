/*
  ServidorHTTP.java
  Carlos Pineda G. 2025
  Modificado para:
  - Enviar Last-Modified en todas las respuestas
  - Procesar If-Modified-Since (304 Not Modified)
  - Servir archivos .html del disco (index.html incluido)
*/

import java.net.ServerSocket;
import java.net.Socket;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.io.PrintWriter;
import java.io.OutputStream;
import java.io.OutputStreamWriter;
import java.io.File;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;

import java.util.Locale;
import java.util.TimeZone;
import java.util.Map;
import java.util.HashMap;

import java.text.SimpleDateFormat;
import java.util.Date;

class ServidorHTTP
{
  // Fecha de "última modificación" para el recurso dinámico /suma (ejemplo simple)
  // Esto permite que el navegador haga una petición condicional y reciba 304 si aplica.
  static final long SERVER_START_MILLIS = System.currentTimeMillis();

  static class Worker extends Thread
  {
    Socket conexion;
    Worker(Socket conexion)
    {
      this.conexion = conexion;
    }

    int valor(String parametros, String variable) throws Exception
    {
      String[] p = parametros.split("&");
      for (int i = 0; i < p.length; i++)
      {
        String[] s = p[i].split("=", 2);
        if (s.length == 2 && s[0].equals(variable))
          return Integer.parseInt(s[1]);
      }
      throw new Exception("Se espera la variable: " + variable);
    }

    private static String formatHTTPDate(long millis)
    {
      // HTTP-date: IMF-fixdate = EEE, dd MMM yyyy HH:mm:ss 'GMT'
      // Usamos SimpleDateFormat con zona horaria GMT
      SimpleDateFormat fmt = new SimpleDateFormat("EEE, dd MMM yyyy HH:mm:ss zzz", Locale.US);
      fmt.setTimeZone(TimeZone.getTimeZone("GMT"));
      return fmt.format(new Date(millis - (millis % 1000))); // resolución a segundos
    }

    private static long parseHTTPDate(String httpDate) throws Exception
    {
      // Intentamos parsear en formato RFC1123
      SimpleDateFormat fmt = new SimpleDateFormat("EEE, dd MMM yyyy HH:mm:ss zzz", Locale.US);
      fmt.setTimeZone(TimeZone.getTimeZone("GMT"));
      Date d = fmt.parse(httpDate);
      // Normalizamos a resolución de segundos (como en los headers)
      return d.getTime() - (d.getTime() % 1000);
    }

    private static Map<String,String> readHeaders(BufferedReader entrada) throws Exception
    {
      Map<String,String> headers = new HashMap<>();
      for (;;)
      {
        String linea = entrada.readLine();
        if (linea == null) break; // conexión cerrada
        if (linea.isEmpty()) break; // fin de los headers
        int idx = linea.indexOf(':');
        if (idx > 0)
        {
          String nombre = linea.substring(0, idx).trim().toLowerCase(Locale.ROOT);
          String valor = linea.substring(idx + 1).trim();
          headers.put(nombre, valor);
        }
      }
      return headers;
    }

    private static void sendHeaders(PrintWriter salida, String statusLine, String[][] headers)
    {
      salida.println(statusLine);
      if (headers != null)
      {
        for (String[] h : headers)
        {
          if (h != null && h.length == 2)
            salida.println(h[0] + ": " + h[1]);
        }
      }
      salida.println();
      salida.flush();
    }

    private static boolean isPathSafe(String path)
    {
      // Evitar path traversal
      if (path.contains("..")) return false;
      // Permitimos solo rutas relativas simples hacia archivos en el directorio actual
      // Eliminamos el primer "/" si existe
      return true;
    }

    private static String getMimeTypeForPath(String localPath)
    {
      // Requisito: .html => text/html
      if (localPath.toLowerCase(Locale.ROOT).endsWith(".html")) return "text/html; charset=utf-8";
      // Por simplicidad, otros tipos no están contemplados en la tarea
      return "application/octet-stream";
    }

    public void run()
    {
      try
      {
        BufferedReader entrada = new BufferedReader(new InputStreamReader(conexion.getInputStream(), StandardCharsets.UTF_8));
        PrintWriter salida = new PrintWriter(new OutputStreamWriter(conexion.getOutputStream(), StandardCharsets.UTF_8), true);
        OutputStream rawOut = conexion.getOutputStream();

        String req = entrada.readLine();
        if (req == null || req.isEmpty())
        {
          return;
        }
        System.out.println("Petición: " + req);

        Map<String,String> headers = readHeaders(entrada);
        headers.forEach((k,v) -> System.out.println("Encabezado: " + k + ": " + v));

        // Parseo de la línea de petición
        String[] parts = req.split(" ");
        if (parts.length < 3)
        {
          String respuesta = "<html><body><h1>400 Bad Request</h1></body></html>";
          byte[] body = respuesta.getBytes(StandardCharsets.UTF_8);
          String now = formatHTTPDate(System.currentTimeMillis());
          sendHeaders(salida, "HTTP/1.1 400 Bad Request", new String[][]{
            {"Date", now},
            {"Content-Type", "text/html; charset=utf-8"},
            {"Content-Length", String.valueOf(body.length)},
            {"Connection", "close"},
            {"Last-Modified", now},
            {"Access-Control-Allow-Origin", "*"}
          });
          rawOut.write(body);
          rawOut.flush();
          return;
        }

        String method = parts[0];
        String fullPath = parts[1];
        String httpVersion = parts[2];

        // Solo soportamos GET
        if (!"GET".equals(method))
        {
          String respuesta = "<html><body><h1>405 Method Not Allowed</h1></body></html>";
          byte[] body = respuesta.getBytes(StandardCharsets.UTF_8);
          String now = formatHTTPDate(System.currentTimeMillis());
          sendHeaders(salida, "HTTP/1.1 405 Method Not Allowed", new String[][]{
            {"Date", now},
            {"Content-Type", "text/html; charset=utf-8"},
            {"Content-Length", String.valueOf(body.length)},
            {"Connection", "close"},
            {"Last-Modified", now},
            {"Access-Control-Allow-Origin", "*"},
            {"Allow", "GET"}
          });
          rawOut.write(body);
          rawOut.flush();
          return;
        }

        String path = fullPath;
        String query = null;
        int qidx = fullPath.indexOf('?');
        if (qidx >= 0)
        {
          path = fullPath.substring(0, qidx);
          query = (qidx + 1 < fullPath.length()) ? fullPath.substring(qidx + 1) : "";
        }

        String ifModifiedSince = headers.get("if-modified-since");
        long ifModifiedSinceMillis = -1L;
        if (ifModifiedSince != null)
        {
          try
          {
            ifModifiedSinceMillis = parseHTTPDate(ifModifiedSince);
          }
          catch (Exception ignore) { /* si falla el parseo, se ignora */ }
        }

        // 1) Recurso dinámico: /suma?a=..&b=..&c=..
        if ("/suma".equals(path) && query != null)
        {
          long resourceLastMod = SERVER_START_MILLIS - (SERVER_START_MILLIS % 1000);
          String lastModifiedStr = formatHTTPDate(resourceLastMod);

          // Si el cliente envió If-Modified-Since y el recurso no ha cambiado desde entonces => 304
          if (ifModifiedSinceMillis >= 0 && resourceLastMod <= ifModifiedSinceMillis)
          {
            String now = formatHTTPDate(System.currentTimeMillis());
            sendHeaders(salida, "HTTP/1.1 304 Not Modified", new String[][]{
              {"Date", now},
              {"Last-Modified", lastModifiedStr},
              {"Connection", "close"},
              {"Content-Length", "0"},
              {"Access-Control-Allow-Origin", "*"}
            });
            return;
          }

          String respuesta;
          try
          {
            respuesta = String.valueOf(valor(query,"a") + valor(query,"b") + valor(query,"c"));
          }
          catch (Exception e)
          {
            respuesta = "Error: " + e.getMessage();
          }

          byte[] body = respuesta.getBytes(StandardCharsets.UTF_8);
          String now = formatHTTPDate(System.currentTimeMillis());
          sendHeaders(salida, "HTTP/1.1 200 OK", new String[][]{
            {"Date", now},
            {"Access-Control-Allow-Origin", "*"},
            {"Content-Type", "text/plain; charset=utf-8"},
            {"Content-Length", String.valueOf(body.length)},
            {"Connection", "close"},
            {"Last-Modified", lastModifiedStr}
          });
          rawOut.write(body);
          rawOut.flush();
          return;
        }

        // 2) Servir archivos .html desde el disco
        // Mapeo raíz "/" a "index.html"
        if ("/".equals(path))
        {
          path = "/index.html";
        }

        // Solo permitimos servir .html según el requisito
        if (path.toLowerCase(Locale.ROOT).endsWith(".html"))
        {
          // Sanitización básica de ruta
          if (!isPathSafe(path))
          {
            String respuesta = "<html><body><h1>403 Forbidden</h1></body></html>";
            byte[] body = respuesta.getBytes(StandardCharsets.UTF_8);
            String now = formatHTTPDate(System.currentTimeMillis());
            sendHeaders(salida, "HTTP/1.1 403 Forbidden", new String[][]{
              {"Date", now},
              {"Content-Type", "text/html; charset=utf-8"},
              {"Content-Length", String.valueOf(body.length)},
              {"Connection", "close"},
              {"Last-Modified", now}
            });
            rawOut.write(body);
            rawOut.flush();
            return;
          }

          // Convertimos la ruta URL a ruta local: quitamos el primer "/"
          String localPath = path.startsWith("/") ? path.substring(1) : path;

          // Bloquear subdirectorios por simplicidad (sirve archivos del directorio actual)
          if (localPath.contains("/") || localPath.contains("\\"))
          {
            String respuesta = "<html><body><h1>404 Not Found</h1></body></html>";
            byte[] body = respuesta.getBytes(StandardCharsets.UTF_8);
            String now = formatHTTPDate(System.currentTimeMillis());
            sendHeaders(salida, "HTTP/1.1 404 Not Found", new String[][]{
              {"Date", now},
              {"Content-Type", "text/html; charset=utf-8"},
              {"Content-Length", String.valueOf(body.length)},
              {"Connection", "close"},
              {"Last-Modified", now}
            });
            rawOut.write(body);
            rawOut.flush();
            return;
          }

          File f = new File(localPath);
          if (!f.exists() || !f.isFile())
          {
            String respuesta = "<html><body><h1>404 Not Found</h1></body></html>";
            byte[] body = respuesta.getBytes(StandardCharsets.UTF_8);
            String now = formatHTTPDate(System.currentTimeMillis());
            sendHeaders(salida, "HTTP/1.1 404 Not Found", new String[][]{
              {"Date", now},
              {"Content-Type", "text/html; charset=utf-8"},
              {"Content-Length", String.valueOf(body.length)},
              {"Connection", "close"},
              {"Last-Modified", now}
            });
            rawOut.write(body);
            rawOut.flush();
            return;
          }

          long fileLastMod = f.lastModified();
          fileLastMod = fileLastMod - (fileLastMod % 1000); // a segundos
          String lastModifiedStr = formatHTTPDate(fileLastMod);

          // If-Modified-Since => 304 Not Modified
          if (ifModifiedSinceMillis >= 0 && fileLastMod <= ifModifiedSinceMillis)
          {
            String now = formatHTTPDate(System.currentTimeMillis());
            sendHeaders(salida, "HTTP/1.1 304 Not Modified", new String[][]{
              {"Date", now},
              {"Last-Modified", lastModifiedStr},
              {"Connection", "close"},
              {"Content-Length", "0"}
            });
            return;
          }

          byte[] body = Files.readAllBytes(Paths.get(localPath));
          String mime = getMimeTypeForPath(localPath);
          String now = formatHTTPDate(System.currentTimeMillis());
          sendHeaders(salida, "HTTP/1.1 200 OK", new String[][]{
            {"Date", now},
            {"Content-Type", mime},
            {"Content-Length", String.valueOf(body.length)},
            {"Connection", "close"},
            {"Last-Modified", lastModifiedStr}
          });
          rawOut.write(body);
          rawOut.flush();
          return;
        }

        // 3) Si no coincide con nada, 404
        {
          String respuesta = "<html><body><h1>404 File Not Found</h1></body></html>";
          byte[] body = respuesta.getBytes(StandardCharsets.UTF_8);
          String now = formatHTTPDate(System.currentTimeMillis());
          sendHeaders(salida, "HTTP/1.1 404 Not Found", new String[][]{
            {"Date", now},
            {"Content-Type", "text/html; charset=utf-8"},
            {"Content-Length", String.valueOf(body.length)},
            {"Connection", "close"},
            {"Last-Modified", now}
          });
          rawOut.write(body);
          rawOut.flush();
        }
      }
      catch (Exception e)
      {
        System.err.println("Error en la conexión: " + e.getMessage());
      }
      finally
      {
        try
        {
          conexion.close();
        }
        catch (Exception e)
        {
          System.err.println("Error en close: " + e.getMessage());
        }
      }
    }
  }

  public static void main(String[] args) throws Exception
  {
    // Nota: el puerto 80 requiere privilegios elevados en muchos SO.
    // Para pruebas locales, puedes cambiar a 8080 y navegar a http://localhost:8080/
    int port = 80;
    if (args.length > 0)
    {
      try { port = Integer.parseInt(args[0]); } catch (Exception ignore) {}
    }
    ServerSocket servidor = new ServerSocket(port);
    System.out.println("Servidor escuchando en puerto " + port);

    for(;;)
    {
      Socket conexion = servidor.accept();
      new Worker(conexion).start();
    }
  }
}