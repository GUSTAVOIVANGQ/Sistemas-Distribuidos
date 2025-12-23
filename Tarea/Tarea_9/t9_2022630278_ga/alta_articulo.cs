// Gestión de artículos - alta_articulo (divide datos y cantidad)
// (c) Alumno 2022630278. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System.Net.Http;

namespace servicio;
public class alta_articulo_ga
{
    class HuboError { public string mensaje; public HuboError(string m){ mensaje=m; } }
    class ArticuloIn { public string? nombre; public string? descripcion; public decimal? precio; public int? cantidad; public string? foto; public int? id_usuario; public string? token; }

    [Function("alta_articulo")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            var art = JsonConvert.DeserializeObject<ArticuloIn>(body);
            if (art == null) throw new Exception("Se esperan los datos del artículo");
            if (string.IsNullOrEmpty(art.nombre)) throw new Exception("Se debe ingresar el nombre del artículo");
            if (string.IsNullOrEmpty(art.descripcion)) throw new Exception("Se debe ingresar la descripción del artículo");
            if (art.precio == null || art.precio <= 0) throw new Exception("El precio debe ser mayor a 0");
            if (art.cantidad == null || art.cantidad < 0) throw new Exception("La cantidad no puede ser negativa");
            if (art.id_usuario == null) throw new Exception("Se debe proporcionar id_usuario");
            if (string.IsNullOrEmpty(art.token)) throw new Exception("Se debe proporcionar token");

            string? Server = Environment.GetEnvironmentVariable("Server");
            string? UserID = Environment.GetEnvironmentVariable("UserID");
            string? Password = Environment.GetEnvironmentVariable("Password");
            string? Database = Environment.GetEnvironmentVariable("Database");
            string? USERS_URL = Environment.GetEnvironmentVariable("USERS_URL");
            string? COMPRAS_URL = Environment.GetEnvironmentVariable("COMPRAS_URL");

            // Verificar acceso vía GU
            using var http = new HttpClient();
            var verUrl = $"{USERS_URL}/api/verifica_acceso?id_usuario={art.id_usuario}&token={Uri.EscapeDataString(art.token!)}";
            var verResp = await http.GetAsync(verUrl);
            if (verResp.StatusCode != System.Net.HttpStatusCode.OK) throw new Exception("Acceso denegado");

            using var conexion = new MySqlConnection($"Server={Server};UserID={UserID};Password={Password};Database={Database};SslMode=Preferred;");
            conexion.Open();

            // Inserta en stock y foto (DB de GA)
            using var tx = conexion.BeginTransaction();
            try
            {
                var cmd1 = new MySqlCommand("INSERT INTO stock (id_articulo,nombre,descripcion,precio) VALUES (0,@n,@d,@p)", conexion, tx);
                cmd1.Parameters.AddWithValue("@n", art.nombre);
                cmd1.Parameters.AddWithValue("@d", art.descripcion);
                cmd1.Parameters.AddWithValue("@p", art.precio);
                cmd1.ExecuteNonQuery();
                long id_articulo = cmd1.LastInsertedId;

                if (art.foto != null)
                {
                    var cmd2 = new MySqlCommand("INSERT INTO fotos_articulos (foto,id_articulo) VALUES (@f,@id)", conexion, tx);
                    cmd2.Parameters.AddWithValue("@f", Convert.FromBase64String(art.foto));
                    cmd2.Parameters.AddWithValue("@id", id_articulo);
                    cmd2.ExecuteNonQuery();
                }

                tx.Commit();

                // Notifica cantidad al microservicio de compras (GC)
                var payload = new { id_articulo = id_articulo, cantidad = art.cantidad, id_usuario = art.id_usuario, token = art.token };
                var resp = await http.PostAsync($"{COMPRAS_URL}/api/alta_articulo",
                    new StringContent(JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json"));
                if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    throw new Exception($"Fallo alta cantidad en GC: {err}");
                }

                return new OkObjectResult("{\"mensaje\":\"Se dio de alta el artículo\",\"id_articulo\":" + id_articulo + "}");
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        catch (Exception e)
        {
            return new BadRequestObjectResult(JsonConvert.SerializeObject(new HuboError(e.Message)));
        }
    }
}