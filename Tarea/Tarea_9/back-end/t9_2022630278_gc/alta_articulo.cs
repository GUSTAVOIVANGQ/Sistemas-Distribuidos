// Gestión de compras - alta de stock.cantidad
// (c) Alumno 2022630278. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System.Net.Http;

namespace servicio;
public class alta_articulo_gc
{
    class HuboError { public string mensaje; public HuboError(string m){ mensaje=m; } }
    class AltaStock { public int? id_articulo; public int? cantidad; public int? id_usuario; public string? token; }

    [Function("alta_articulo")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            var d = JsonConvert.DeserializeObject<AltaStock>(body);
            if (d == null) throw new Exception("Se esperan los datos");
            if (d.id_articulo == null) throw new Exception("Se debe ingresar id_articulo");
            if (d.cantidad == null || d.cantidad < 0) throw new Exception("La cantidad no puede ser negativa");
            if (d.id_usuario == null) throw new Exception("Se debe proporcionar id_usuario");
            if (string.IsNullOrEmpty(d.token)) throw new Exception("Se debe proporcionar token");

            string? Server = Environment.GetEnvironmentVariable("Server");
            string? UserID = Environment.GetEnvironmentVariable("UserID");
            string? Password = Environment.GetEnvironmentVariable("Password");
            string? Database = Environment.GetEnvironmentVariable("Database");
            string? USERS_URL = Environment.GetEnvironmentVariable("USERS_URL");

            // Verifica acceso vía GU
            using var http = new HttpClient();
            var verResp = await http.GetAsync($"{USERS_URL}/api/verifica_acceso?id_usuario={d.id_usuario}&token={Uri.EscapeDataString(d.token!)}");
            if (verResp.StatusCode != System.Net.HttpStatusCode.OK) throw new Exception("Acceso denegado");

            using var conexion = new MySqlConnection($"Server={Server};UserID={UserID};Password={Password};Database={Database};SslMode=Preferred;");
            conexion.Open();

            // Inserta o actualiza cantidad
            // Si ya existe id_articulo, actualiza; si no, inserta.
            // stock tiene UNIQUE(id_articulo)
            try
            {
                var cmdSel = new MySqlCommand("SELECT cantidad FROM stock WHERE id_articulo=@id", conexion);
                cmdSel.Parameters.AddWithValue("@id", d.id_articulo);
                var r = cmdSel.ExecuteReader();
                bool existe = r.Read();
                r.Close();

                if (existe)
                {
                    var cmdUpd = new MySqlCommand("UPDATE stock SET cantidad=@c WHERE id_articulo=@id", conexion);
                    cmdUpd.Parameters.AddWithValue("@c", d.cantidad);
                    cmdUpd.Parameters.AddWithValue("@id", d.id_articulo);
                    cmdUpd.ExecuteNonQuery();
                }
                else
                {
                    var cmdIns = new MySqlCommand("INSERT INTO stock (id_articulo,cantidad) VALUES (@id,@c)", conexion);
                    cmdIns.Parameters.AddWithValue("@id", d.id_articulo);
                    cmdIns.Parameters.AddWithValue("@c", d.cantidad);
                    cmdIns.ExecuteNonQuery();
                }

                return new OkObjectResult("{\"mensaje\":\"Stock registrado/actualizado en GC\"}");
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