// Tarea 8 - Eliminar todo el carrito del usuario (y regresar stock)
// (c) Alumno 2022630278. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

namespace servicio;
public class elimina_carrito_compra
{
    class HuboError
    {
        public string mensaje;
        public HuboError(string mensaje) { this.mensaje = mensaje; }
    }

    [Function("elimina_carrito_compra")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "delete")] HttpRequest req)
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

            var tx = conexion.BeginTransaction();
            try
            {
                // Obtener todos los artículos del carrito
                var cmdSel = new MySqlCommand("SELECT id_articulo, cantidad FROM carrito_compra WHERE id_usuario=@id_usuario", conexion, tx);
                cmdSel.Parameters.AddWithValue("@id_usuario", id_usuario);
                var r = cmdSel.ExecuteReader();
                var items = new List<(int id_articulo, int cantidad)>();
                while (r.Read())
                {
                    items.Add((r.GetInt32(0), r.GetInt32(1)));
                }
                r.Close();

                // Regresar cantidades al stock
                foreach (var item in items)
                {
                    var cmdUpd = new MySqlCommand("UPDATE stock SET cantidad=cantidad+@cant WHERE id_articulo=@id_articulo", conexion, tx);
                    cmdUpd.Parameters.AddWithValue("@cant", item.cantidad);
                    cmdUpd.Parameters.AddWithValue("@id_articulo", item.id_articulo);
                    cmdUpd.ExecuteNonQuery();
                }

                // Borrar todos del carrito
                var cmdDel = new MySqlCommand("DELETE FROM carrito_compra WHERE id_usuario=@id_usuario", conexion, tx);
                cmdDel.Parameters.AddWithValue("@id_usuario", id_usuario);
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