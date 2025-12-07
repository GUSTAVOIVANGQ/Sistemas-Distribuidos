// Tarea 8 - Finalizar compra (checkout de carrito)
// (c) Alumno 2022630278. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

namespace servicio;
public class finaliza_compra
{
    class HuboError
    {
        public string mensaje;
        public HuboError(string mensaje) { this.mensaje = mensaje; }
    }
    class Resultado
    {
        public long id_orden;
        public decimal total;
    }

    [Function("finaliza_compra")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
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
            string cs = "Server=" + Server + ";UserID=" + UserID + ";Password=" + Password + ";" + "Database=" + Database + ";SslMode=Preferred;";
            using var conexion = new MySqlConnection(cs);
            conexion.Open();

            if (!login.verifica_acceso(conexion, id_usuario, token!)) throw new Exception("Acceso denegado");

            // Leer carrito con precios actuales
            var cmdCar = new MySqlCommand(
                "SELECT cc.id_articulo, cc.cantidad, s.precio " +
                "FROM carrito_compra cc JOIN stock s ON s.id_articulo = cc.id_articulo " +
                "WHERE cc.id_usuario=@id_usuario", conexion);
            cmdCar.Parameters.AddWithValue("@id_usuario", id_usuario);
            var r = cmdCar.ExecuteReader();
            var items = new List<(int id_articulo, int cantidad, decimal precio)>();
            while (r.Read())
            {
                items.Add((r.GetInt32(0), r.GetInt32(1), r.GetDecimal(2)));
            }
            r.Close();

            if (items.Count == 0)
                return new BadRequestObjectResult(JsonConvert.SerializeObject(new HuboError("El carrito está vacío")));

            decimal total = 0m;
            foreach (var it in items) total += it.precio * it.cantidad;

            var tx = conexion.BeginTransaction();
            try
            {
                // Crear orden
                var cmdOrd = new MySqlCommand("INSERT INTO ordenes (id_orden, id_usuario, fecha, total) VALUES (0, @id_usuario, @fecha, @total)", conexion, tx);
                cmdOrd.Parameters.AddWithValue("@id_usuario", id_usuario);
                cmdOrd.Parameters.AddWithValue("@fecha", DateTime.UtcNow);
                cmdOrd.Parameters.AddWithValue("@total", total);
                cmdOrd.ExecuteNonQuery();
                long id_orden = cmdOrd.LastInsertedId;

                // Detalles
                foreach (var it in items)
                {
                    var cmdDet = new MySqlCommand(
                        "INSERT INTO orden_detalle (id_orden, id_articulo, cantidad, precio_unitario, subtotal) " +
                        "VALUES (@id_orden, @id_articulo, @cantidad, @precio, @subtotal)", conexion, tx);
                    cmdDet.Parameters.AddWithValue("@id_orden", id_orden);
                    cmdDet.Parameters.AddWithValue("@id_articulo", it.id_articulo);
                    cmdDet.Parameters.AddWithValue("@cantidad", it.cantidad);
                    cmdDet.Parameters.AddWithValue("@precio", it.precio);
                    cmdDet.Parameters.AddWithValue("@subtotal", it.precio * it.cantidad);
                    cmdDet.ExecuteNonQuery();
                }

                // Vaciar carrito (sin regresar stock)
                var cmdDel = new MySqlCommand("DELETE FROM carrito_compra WHERE id_usuario=@id_usuario", conexion, tx);
                cmdDel.Parameters.AddWithValue("@id_usuario", id_usuario);
                cmdDel.ExecuteNonQuery();

                tx.Commit();
                var ok = new Resultado { id_orden = id_orden, total = total };
                return new OkObjectResult(JsonConvert.SerializeObject(ok));
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