// Gestión de compras - Eliminar todo el carrito (y regresar stock)
// (c) Alumno 2022630278. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System.Net.Http;

namespace servicio;
public class elimina_carrito_compra
{
    class HuboError { public string mensaje; public HuboError(string m){ mensaje=m; } }

    [Function("elimina_carrito_compra")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "delete")] HttpRequest req)
    {
        try
            {
                string? id_usuario_s = req.Query["id_usuario"];
                string? token = req.Query["token"];
                if (string.IsNullOrEmpty(id_usuario_s)) throw new Exception("Se debe proporcionar id_usuario");
                if (string.IsNullOrEmpty(token)) throw new Exception("Se debe proporcionar token");
                int id_usuario = int.Parse(id_usuario_s!);

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
                    var cmdSel = new MySqlCommand("SELECT id_articulo, cantidad FROM carrito_compra WHERE id_usuario=@u", conexion, tx);
                    cmdSel.Parameters.AddWithValue("@u", id_usuario);
                    var items = new List<(int id_articulo, int cantidad)>();
                    using (var r = cmdSel.ExecuteReader())
                    {
                        while (r.Read())
                            items.Add((r.GetInt32(0), r.GetInt32(1)));
                    }

                    foreach (var item in items)
                    {
                        var cmdUpd = new MySqlCommand("UPDATE stock SET cantidad=cantidad+@c WHERE id_articulo=@a", conexion, tx);
                        cmdUpd.Parameters.AddWithValue("@c", item.cantidad);
                        cmdUpd.Parameters.AddWithValue("@a", item.id_articulo);
                        cmdUpd.ExecuteNonQuery();
                    }

                    var cmdDel = new MySqlCommand("DELETE FROM carrito_compra WHERE id_usuario=@u", conexion, tx);
                    cmdDel.Parameters.AddWithValue("@u", id_usuario);
                    cmdDel.ExecuteNonQuery();

                    tx.Commit();
                    return new OkObjectResult("{\"mensaje\":\"Carrito eliminado\"}");
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