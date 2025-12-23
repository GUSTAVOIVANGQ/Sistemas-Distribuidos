// Gestión de compras - Compra de artículo (carrito_compra + stock.cantidad)
// (c) Alumno 2022630278. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System.Net.Http;

namespace servicio;
public class compra_articulo
{
    class HuboError { public string mensaje; public HuboError(string m){ mensaje=m; } }
    class Compra { public int? id_articulo; public int? cantidad; public int? id_usuario; public string? token; }

    [Function("compra_articulo")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            var c = JsonConvert.DeserializeObject<Compra>(body);
            if (c == null) throw new Exception("Se esperan los datos de compra");
            if (c.id_articulo == null) throw new Exception("Se debe ingresar id_articulo");
            if (c.cantidad == null || c.cantidad <= 0) throw new Exception("La cantidad debe ser mayor a 0");
            if (c.id_usuario == null) throw new Exception("Se debe proporcionar id_usuario");
            if (string.IsNullOrEmpty(c.token)) throw new Exception("Se debe proporcionar token");

            string? Server = Environment.GetEnvironmentVariable("Server");
            string? UserID = Environment.GetEnvironmentVariable("UserID");
            string? Password = Environment.GetEnvironmentVariable("Password");
            string? Database = Environment.GetEnvironmentVariable("Database");
            string? USERS_URL = Environment.GetEnvironmentVariable("USERS_URL");

            using var http = new HttpClient();
            var verResp = await http.GetAsync($"{USERS_URL}/api/verifica_acceso?id_usuario={c.id_usuario}&token={Uri.EscapeDataString(c.token!)}");
            if (verResp.StatusCode != System.Net.HttpStatusCode.OK) throw new Exception("Acceso denegado");

            using var conexion = new MySqlConnection($"Server={Server};UserID={UserID};Password={Password};Database={Database};SslMode=Preferred;");
            conexion.Open();

            // Verificar stock actual (sólo cantidad)
            int existencia = 0;
            var cmd0 = new MySqlCommand("SELECT cantidad FROM stock WHERE id_articulo=@id", conexion);
            cmd0.Parameters.AddWithValue("@id", c.id_articulo);
            using (var r0 = cmd0.ExecuteReader())
            {
                bool existeArt = r0.Read();
                if (!existeArt) throw new Exception("El artículo no existe");
                existencia = r0.GetInt32(0);
            }

            if (c.cantidad!.Value > existencia)
                return new BadRequestObjectResult(JsonConvert.SerializeObject(new HuboError("No hay suficientes artículos en stock")));

            // Transacción: insertar/actualizar carrito y restar stock
            using var tx = conexion.BeginTransaction();
            try
            {
                var cmdSel = new MySqlCommand("SELECT cantidad FROM carrito_compra WHERE id_usuario=@u AND id_articulo=@a", conexion, tx);
                cmdSel.Parameters.AddWithValue("@u", c.id_usuario);
                cmdSel.Parameters.AddWithValue("@a", c.id_articulo);
                int cantidadActual = 0;
                bool yaExiste;
                using (var r = cmdSel.ExecuteReader())
                {
                    yaExiste = r.Read();
                    if (yaExiste) cantidadActual = r.GetInt32(0);
                }

                if (yaExiste)
                {
                    var cmdUpdCar = new MySqlCommand("UPDATE carrito_compra SET cantidad=@c WHERE id_usuario=@u AND id_articulo=@a", conexion, tx);
                    cmdUpdCar.Parameters.AddWithValue("@c", cantidadActual + c.cantidad);
                    cmdUpdCar.Parameters.AddWithValue("@u", c.id_usuario);
                    cmdUpdCar.Parameters.AddWithValue("@a", c.id_articulo);
                    cmdUpdCar.ExecuteNonQuery();
                }
                else
                {
                    var cmdInsCar = new MySqlCommand("INSERT INTO carrito_compra (id_usuario,id_articulo,cantidad) VALUES (@u,@a,@c)", conexion, tx);
                    cmdInsCar.Parameters.AddWithValue("@u", c.id_usuario);
                    cmdInsCar.Parameters.AddWithValue("@a", c.id_articulo);
                    cmdInsCar.Parameters.AddWithValue("@c", c.cantidad);
                    cmdInsCar.ExecuteNonQuery();
                }

                var cmdUpdStock = new MySqlCommand("UPDATE stock SET cantidad=cantidad-@c WHERE id_articulo=@a", conexion, tx);
                cmdUpdStock.Parameters.AddWithValue("@c", c.cantidad);
                cmdUpdStock.Parameters.AddWithValue("@a", c.id_articulo);
                cmdUpdStock.ExecuteNonQuery();

                tx.Commit();
                return new OkObjectResult("{\"mensaje\":\"Compra registrada\"}");
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