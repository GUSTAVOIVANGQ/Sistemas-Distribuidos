// Gestión de compras - Eliminar artículo del carrito (y regresar stock)
// (c) Alumno 2022630278. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System.Net.Http;

namespace servicio;
public class elimina_articulo_carrito_compra
{
    class HuboError { public string mensaje; public HuboError(string m){ mensaje=m; } }

    [Function("elimina_articulo_carrito_compra")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "delete")] HttpRequest req)
    {
        try
        {
            string? id_usuario_s = req.Query["id_usuario"];
            string? id_articulo_s = req.Query["id_articulo"];
            string? token = req.Query["token"];
            if (string.IsNullOrEmpty(id_usuario_s)) throw new Exception("Se debe proporcionar id_usuario");
            if (string.IsNullOrEmpty(id_articulo_s)) throw new Exception("Se debe proporcionar id_articulo");
            if (string.IsNullOrEmpty(token)) throw new Exception("Se debe proporcionar token");
            int id_usuario = int.Parse(id_usuario_s!);
            int id_articulo = int.Parse(id_articulo_s!);

            string? Server = Environment.GetEnvironmentVariable("Server");
            string? UserID = Environment.GetEnvironmentVariable("UserID");
            string? Password = Environment.GetEnvironmentVariable("Password");
            string? Database = Environment.GetEnvironmentVariable("Database");
            string? USERS_URL = Environment.GetEnvironmentVariable("USERS_URL");

            using var http = new HttpClient();
            var verResp = await http.GetAsync($"{USERS_URL}/api/verifica_acceso?id_usuario={id_usuario}&token={Uri.EscapeDataString(token!)}");
            if (verResp.StatusCode != System.Net.HttpStatusCode.OK) throw new Exception("Acceso denegado");

            using var conexion = new MySqlConnection($"Server={Server};UserID={UserID};Password={Password};Database={Database};SslMode=Preferred;");
            conexion.Open();

            using var tx = conexion.BeginTransaction();
            try
            {
                var cmdSel = new MySqlCommand("SELECT cantidad FROM carrito_compra WHERE id_usuario=@u AND id_articulo=@a", conexion, tx);
                cmdSel.Parameters.AddWithValue("@u", id_usuario);
                cmdSel.Parameters.AddWithValue("@a", id_articulo);
                int cant;
                using (var r = cmdSel.ExecuteReader())
                {
                    if (!r.Read()) throw new Exception("El artículo no está en el carrito");
                    cant = r.GetInt32(0);
                }

                var cmdUpdStock = new MySqlCommand("UPDATE stock SET cantidad=cantidad+@c WHERE id_articulo=@a", conexion, tx);
                cmdUpdStock.Parameters.AddWithValue("@c", cant);
                cmdUpdStock.Parameters.AddWithValue("@a", id_articulo);
                cmdUpdStock.ExecuteNonQuery();

                var cmdDel = new MySqlCommand("DELETE FROM carrito_compra WHERE id_usuario=@u AND id_articulo=@a", conexion, tx);
                cmdDel.Parameters.AddWithValue("@u", id_usuario);
                cmdDel.Parameters.AddWithValue("@a", id_articulo);
                cmdDel.ExecuteNonQuery();

                tx.Commit();
                return new OkObjectResult("{\"mensaje\":\"Artículo eliminado del carrito\"}");
            }
            catch (Exception ex)
            {
                tx.Rollback();
                throw new Exception(ex.Message);
            }
        }
        catch (Exception e)
        {
            return new BadRequestObjectResult(JsonConvert.SerializeObject(new HuboError(e.Message)));
        }
    }
}