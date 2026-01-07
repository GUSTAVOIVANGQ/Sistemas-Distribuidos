// Gestión de compras - Modificar cantidad del carrito (+1 / -1) y ajustar stock
// (c) Alumno 2022630278. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System.Net.Http;

namespace servicio;
public class modifica_carrito_compra
{
    class HuboError { public string mensaje; public HuboError(string m){ mensaje=m; } }
    class Modificacion { public int? id_articulo; public int? incremento; public int? id_usuario; public string? token; }

    [Function("modifica_carrito_compra")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "put")] HttpRequest req)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            var m = JsonConvert.DeserializeObject<Modificacion>(body);
            if (m == null) throw new Exception("Se esperan los datos de modificación");
            if (m.id_articulo == null) throw new Exception("Se debe proporcionar id_articulo");
            if (m.incremento == null || (m.incremento != 1 && m.incremento != -1)) throw new Exception("El incremento debe ser +1 o -1");
            if (m.id_usuario == null) throw new Exception("Se debe proporcionar id_usuario");
            if (string.IsNullOrEmpty(m.token)) throw new Exception("Se debe proporcionar token");

            string? Server = Environment.GetEnvironmentVariable("Server");
            string? UserID = Environment.GetEnvironmentVariable("UserID");
            string? Password = Environment.GetEnvironmentVariable("Password");
            string? Database = Environment.GetEnvironmentVariable("Database");
            string? USERS_URL = Environment.GetEnvironmentVariable("USERS_URL");

            using var http = new HttpClient();
            var verResp = await http.GetAsync($"{USERS_URL}/api/verifica_acceso?id_usuario={m.id_usuario}&token={Uri.EscapeDataString(m.token!)}");
            if (verResp.StatusCode != System.Net.HttpStatusCode.OK) throw new Exception("Acceso denegado");

            using var conexion = new MySqlConnection($"Server={Server};UserID={UserID};Password={Password};Database={Database};SslMode=Preferred;");
            conexion.Open();

            using var tx = conexion.BeginTransaction();
            try
            {
                var cmdSel = new MySqlCommand("SELECT cantidad FROM carrito_compra WHERE id_usuario=@u AND id_articulo=@a", conexion, tx);
                cmdSel.Parameters.AddWithValue("@u", m.id_usuario);
                cmdSel.Parameters.AddWithValue("@a", m.id_articulo);
                int cantCarrito = 0;
                bool existe;
                using (var r = cmdSel.ExecuteReader())
                {
                    existe = r.Read();
                    if (existe) cantCarrito = r.GetInt32(0);
                }

                if (!existe)
                {
                    tx.Rollback();
                    throw new Exception("El artículo no está en el carrito");
                }

                if (m.incremento == 1)
                {
                    var cmdStock = new MySqlCommand("SELECT cantidad FROM stock WHERE id_articulo=@a", conexion, tx);
                    cmdStock.Parameters.AddWithValue("@a", m.id_articulo);
                    int existencia;
                    using (var r2 = cmdStock.ExecuteReader())
                    {
                        if (!r2.Read())
                        {
                            tx.Rollback();
                            throw new Exception("El artículo no existe");
                        }
                        existencia = r2.GetInt32(0);
                    }

                    if (existencia <= 0)
                    {
                        tx.Rollback();
                        return new BadRequestObjectResult(JsonConvert.SerializeObject(new HuboError("No hay suficientes artículos en stock")));
                    }

                    var cmdUpdCar = new MySqlCommand("UPDATE carrito_compra SET cantidad=cantidad+1 WHERE id_usuario=@u AND id_articulo=@a", conexion, tx);
                    cmdUpdCar.Parameters.AddWithValue("@u", m.id_usuario);
                    cmdUpdCar.Parameters.AddWithValue("@a", m.id_articulo);
                    cmdUpdCar.ExecuteNonQuery();

                    var cmdUpdStock = new MySqlCommand("UPDATE stock SET cantidad=cantidad-1 WHERE id_articulo=@a", conexion, tx);
                    cmdUpdStock.Parameters.AddWithValue("@a", m.id_articulo);
                    cmdUpdStock.ExecuteNonQuery();
                }
                else
                {
                    if (cantCarrito <= 0)
                    {
                        tx.Rollback();
                        return new BadRequestObjectResult(JsonConvert.SerializeObject(new HuboError("No hay más artículos en el carrito")));
                    }

                    var cmdUpdCar = new MySqlCommand("UPDATE carrito_compra SET cantidad=cantidad-1 WHERE id_usuario=@u AND id_articulo=@a", conexion, tx);
                    cmdUpdCar.Parameters.AddWithValue("@u", m.id_usuario);
                    cmdUpdCar.Parameters.AddWithValue("@a", m.id_articulo);
                    cmdUpdCar.ExecuteNonQuery();

                    var cmdZeroSel = new MySqlCommand("SELECT cantidad FROM carrito_compra WHERE id_usuario=@u AND id_articulo=@a", conexion, tx);
                    cmdZeroSel.Parameters.AddWithValue("@u", m.id_usuario);
                    cmdZeroSel.Parameters.AddWithValue("@a", m.id_articulo);
                    int nuevaCant;
                    using (var r3 = cmdZeroSel.ExecuteReader())
                    {
                        r3.Read();
                        nuevaCant = r3.GetInt32(0);
                    }
                    if (nuevaCant < 0)
                    {
                        tx.Rollback();
                        throw new Exception("Cantidad inválida en carrito");
                    }

                    var cmdUpdStock = new MySqlCommand("UPDATE stock SET cantidad=cantidad+1 WHERE id_articulo=@a", conexion, tx);
                    cmdUpdStock.Parameters.AddWithValue("@a", m.id_articulo);
                    cmdUpdStock.ExecuteNonQuery();

                    if (nuevaCant == 0)
                    {
                        var cmdDel = new MySqlCommand("DELETE FROM carrito_compra WHERE id_usuario=@u AND id_articulo=@a", conexion, tx);
                        cmdDel.Parameters.AddWithValue("@u", m.id_usuario);
                        cmdDel.Parameters.AddWithValue("@a", m.id_articulo);
                        cmdDel.ExecuteNonQuery();
                    }
                }

                tx.Commit();
                return new OkObjectResult("{\"mensaje\":\"Se modificó la cantidad del carrito\"}");
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