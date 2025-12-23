// Devuelve nombre, precio y foto por id_articulo (uso entre microservicios)
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

namespace servicio;
public class consulta_articulo_por_id
{
    class HuboError { public string mensaje; public HuboError(string m){ mensaje=m; } }
    class Articulo { public int id_articulo; public string nombre=""; public decimal precio; public string? foto; }

    [Function("consulta_articulo_por_id")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        try
        {
            string? id_s = req.Query["id_articulo"];
            if (string.IsNullOrEmpty(id_s)) throw new Exception("Se debe proporcionar id_articulo");
            int id = int.Parse(id_s);

            string? Server = Environment.GetEnvironmentVariable("Server");
            string? UserID = Environment.GetEnvironmentVariable("UserID");
            string? Password = Environment.GetEnvironmentVariable("Password");
            string? Database = Environment.GetEnvironmentVariable("Database");

            using var conexion = new MySqlConnection($"Server={Server};UserID={UserID};Password={Password};Database={Database};SslMode=Preferred;");
            conexion.Open();

            var cmd = new MySqlCommand(
                "SELECT s.id_articulo, s.nombre, s.precio, fa.foto, LENGTH(fa.foto) " +
                "FROM stock s LEFT JOIN fotos_articulos fa ON fa.id_articulo=s.id_articulo " +
                "WHERE s.id_articulo=@id", conexion);
            cmd.Parameters.AddWithValue("@id", id);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) throw new Exception("El artículo no existe");
            var a = new Articulo();
            a.id_articulo = r.GetInt32(0);
            a.nombre = r.GetString(1);
            a.precio = r.GetDecimal(2);
            if (!r.IsDBNull(3))
            {
                int len = r.GetInt32(4);
                byte[] foto = new byte[len];
                r.GetBytes(3, 0, foto, 0, len);
                a.foto = Convert.ToBase64String(foto);
            }
            return new OkObjectResult(JsonConvert.SerializeObject(a));
        }
        catch (Exception e)
        {
            return new BadRequestObjectResult(JsonConvert.SerializeObject(new HuboError(e.Message)));
        }
    }
}