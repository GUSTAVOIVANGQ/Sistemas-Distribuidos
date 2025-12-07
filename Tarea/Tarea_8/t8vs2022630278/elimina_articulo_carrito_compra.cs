// Tarea 8 - Eliminar un artículo del carrito (y regresar stock)
// (c) Alumno 2022630278. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

namespace servicio;
public class elimina_articulo_carrito_compra
{
    class HuboError
    {
        public string mensaje;
        public HuboError(string mensaje) { this.mensaje = mensaje; }
    }

    [Function("elimina_articulo_carrito_compra")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "delete")] HttpRequest req)
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
            string cs = "Server=" + Server + ";UserID=" + UserID + ";Password=" + Password + ";" + "Database=" + Database + ";SslMode=Preferred;";
            using var conexion = new MySqlConnection(cs);
            conexion.Open();

            if (!login.verifica_acceso(conexion, id_usuario, token!)) throw new Exception("Acceso denegado");

            var tx = conexion.BeginTransaction();
            try
            {
                // Obtener cantidad en carrito para ese artículo
                var cmdSel = new MySqlCommand("SELECT cantidad FROM carrito_compra WHERE id_usuario=@id_usuario AND id_articulo=@id_articulo", conexion, tx);
                cmdSel.Parameters.AddWithValue("@id_usuario", id_usuario);
                cmdSel.Parameters.AddWithValue("@id_articulo", id_articulo);
                var r = cmdSel.ExecuteReader();
                if (!r.Read())
                {
                    r.Close();
                    tx.Rollback();
                    throw new Exception("El artículo no está en el carrito");
                }
                int cant = r.GetInt32(0);
                r.Close();

                // Regresar al stock
                var cmdUpdStock = new MySqlCommand("UPDATE stock SET cantidad=cantidad+@cant WHERE id_articulo=@id_articulo", conexion, tx);
                cmdUpdStock.Parameters.AddWithValue("@cant", cant);
                cmdUpdStock.Parameters.AddWithValue("@id_articulo", id_articulo);
                cmdUpdStock.ExecuteNonQuery();

                // Borrar del carrito
                var cmdDel = new MySqlCommand("DELETE FROM carrito_compra WHERE id_usuario=@id_usuario AND id_articulo=@id_articulo", conexion, tx);
                cmdDel.Parameters.AddWithValue("@id_usuario", id_usuario);
                cmdDel.Parameters.AddWithValue("@id_articulo", id_articulo);
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