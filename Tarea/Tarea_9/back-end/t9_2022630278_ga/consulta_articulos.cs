// Consulta de artículos con LIKE
// (c) Alumno 2022630278. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System.Net.Http;

namespace servicio;
public class consulta_articulos_ga
{
    class HuboError { public string mensaje; public HuboError(string m){ mensaje=m; } }
    class Articulo { public int id_articulo; public string? foto; public string nombre=""; public string descripcion=""; public decimal precio; }

    [Function("consulta_articulos")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        try
        {
            string? palabra = req.Query["palabra_clave"];
            string? id_usuario_s = req.Query["id_usuario"];
            string? token = req.Query["token"];
            if (string.IsNullOrEmpty(palabra)) throw new Exception("Se debe ingresar la palabra_clave");
            if (string.IsNullOrEmpty(id_usuario_s)) throw new Exception("Se debe proporcionar id_usuario");
            if (string.IsNullOrEmpty(token)) throw new Exception("Se debe proporcionar token");
            int id_usuario = int.Parse(id_usuario_s!);

            string? Server = Environment.GetEnvironmentVariable("Server");
            string? UserID = Environment.GetEnvironmentVariable("UserID");
            string? Password = Environment.GetEnvironmentVariable("Password");
            string? Database = Environment.GetEnvironmentVariable("Database");
            string? USERS_URL = Environment.GetEnvironmentVariable("USERS_URL");

            // Verifica acceso vía GU
            using var http = new HttpClient();
            var verResp = http.GetAsync($"{USERS_URL}/api/verifica_acceso?id_usuario={id_usuario}&token={Uri.EscapeDataString(token!)}").Result;
            if (verResp.StatusCode != System.Net.HttpStatusCode.OK) throw new Exception("Acceso denegado");

            using var conexion = new MySqlConnection($"Server={Server};UserID={UserID};Password={Password};Database={Database};SslMode=Preferred;");
            conexion.Open();

            var cmd = new MySqlCommand(
                "SELECT s.id_articulo, fa.foto, LENGTH(fa.foto), s.nombre, s.descripcion, s.precio " +
                "FROM stock s LEFT OUTER JOIN fotos_articulos fa ON s.id_articulo = fa.id_articulo " +
                "WHERE s.nombre LIKE @like OR s.descripcion LIKE @like", conexion);
            cmd.Parameters.AddWithValue("@like", "%" + palabra + "%");

            using var r = cmd.ExecuteReader();
            var lista = new List<Articulo>();
            while (r.Read())
            {
                var a = new Articulo();
                a.id_articulo = r.GetInt32(0);
                if (!r.IsDBNull(1))
                {
                    int len = r.GetInt32(2);
                    byte[] foto = new byte[len];
                    r.GetBytes(1, 0, foto, 0, len);
                    a.foto = Convert.ToBase64String(foto);
                }
                a.nombre = r.GetString(3);
                a.descripcion = r.GetString(4);
                a.precio = r.GetDecimal(5);
                lista.Add(a);
            }
            return new OkObjectResult(JsonConvert.SerializeObject(lista));
        }
        catch (Exception e)
        {
            return new BadRequestObjectResult(JsonConvert.SerializeObject(new HuboError(e.Message)));
        }
    }
}