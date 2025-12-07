// Tarea 8 - Modificar cantidad del carrito (+1 / -1) y ajustar stock
// (c) Alumno 2022630278. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

namespace servicio;
public class modifica_carrito_compra
{
    class HuboError
    {
        public string mensaje;
        public HuboError(string mensaje) { this.mensaje = mensaje; }
    }
    class Modificacion
    {
        public int? id_articulo;
        public int? incremento; // +1 o -1
        public int? id_usuario;
        public string? token;
    }

    [Function("modifica_carrito_compra")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "put")] HttpRequest req)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            Modificacion? m = JsonConvert.DeserializeObject<Modificacion>(body);
            if (m == null) throw new Exception("Se esperan los datos de modificación");
            if (m.id_articulo == null) throw new Exception("Se debe proporcionar id_articulo");
            if (m.incremento == null || (m.incremento != 1 && m.incremento != -1)) throw new Exception("El incremento debe ser +1 o -1");
            if (m.id_usuario == null) throw new Exception("Se debe proporcionar id_usuario");
            if (string.IsNullOrEmpty(m.token)) throw new Exception("Se debe proporcionar token");

            string? Server = Environment.GetEnvironmentVariable("Server");
            string? UserID = Environment.GetEnvironmentVariable("UserID");
            string? Password = Environment.GetEnvironmentVariable("Password");
            string? Database = Environment.GetEnvironmentVariable("Database");
            string cs = "Server=" + Server + ";UserID=" + UserID + ";Password=" + Password + ";" + "Database=" + Database + ";SslMode=Preferred;";
            using var conexion = new MySqlConnection(cs);
            conexion.Open();

            if (!login.verifica_acceso(conexion, m.id_usuario!.Value, m.token!)) throw new Exception("Acceso denegado");

            var tx = conexion.BeginTransaction();
            try
            {
                // Obtener cantidad actual en carrito
                var cmdSel = new MySqlCommand("SELECT cantidad FROM carrito_compra WHERE id_usuario=@id_usuario AND id_articulo=@id_articulo", conexion, tx);
                cmdSel.Parameters.AddWithValue("@id_usuario", m.id_usuario);
                cmdSel.Parameters.AddWithValue("@id_articulo", m.id_articulo);
                var r = cmdSel.ExecuteReader();
                bool existe = r.Read();
                int cantCarrito = existe ? r.GetInt32(0) : 0;
                r.Close();

                if (!existe)
                {
                    tx.Rollback();
                    throw new Exception("El artículo no está en el carrito");
                }

                if (m.incremento == 1)
                {
                    // Verificar stock disponible
                    var cmdStock = new MySqlCommand("SELECT cantidad FROM stock WHERE id_articulo=@id_articulo", conexion, tx);
                    cmdStock.Parameters.AddWithValue("@id_articulo", m.id_articulo);
                    var r2 = cmdStock.ExecuteReader();
                    if (!r2.Read())
                    {
                        r2.Close();
                        tx.Rollback();
                        throw new Exception("El artículo no existe");
                    }
                    int existencia = r2.GetInt32(0);
                    r2.Close();

                    if (existencia <= 0)
                    {
                        tx.Rollback();
                        return new BadRequestObjectResult(JsonConvert.SerializeObject(new HuboError("No hay suficientes artículos en stock")));
                    }

                    // Ajustar carrito +1 y stock -1
                    var cmdUpdCar = new MySqlCommand("UPDATE carrito_compra SET cantidad=cantidad+1 WHERE id_usuario=@id_usuario AND id_articulo=@id_articulo", conexion, tx);
                    cmdUpdCar.Parameters.AddWithValue("@id_usuario", m.id_usuario);
                    cmdUpdCar.Parameters.AddWithValue("@id_articulo", m.id_articulo);
                    cmdUpdCar.ExecuteNonQuery();

                    var cmdUpdStock = new MySqlCommand("UPDATE stock SET cantidad=cantidad-1 WHERE id_articulo=@id_articulo", conexion, tx);
                    cmdUpdStock.Parameters.AddWithValue("@id_articulo", m.id_articulo);
                    cmdUpdStock.ExecuteNonQuery();
                }
                else // -1
                {
                    if (cantCarrito <= 0)
                    {
                        tx.Rollback();
                        return new BadRequestObjectResult(JsonConvert.SerializeObject(new HuboError("No hay más artículos en el carrito")));
                    }

                    // Ajustar carrito -1 y stock +1
                    var cmdUpdCar = new MySqlCommand("UPDATE carrito_compra SET cantidad=cantidad-1 WHERE id_usuario=@id_usuario AND id_articulo=@id_articulo", conexion, tx);
                    cmdUpdCar.Parameters.AddWithValue("@id_usuario", m.id_usuario);
                    cmdUpdCar.Parameters.AddWithValue("@id_articulo", m.id_articulo);
                    cmdUpdCar.ExecuteNonQuery();

                    // Si quedó en 0, se podría opcionalmente eliminar el registro; la consigna no lo exige, pero se deja en 0.
                    var cmdZeroSel = new MySqlCommand("SELECT cantidad FROM carrito_compra WHERE id_usuario=@id_usuario AND id_articulo=@id_articulo", conexion, tx);
                    cmdZeroSel.Parameters.AddWithValue("@id_usuario", m.id_usuario);
                    cmdZeroSel.Parameters.AddWithValue("@id_articulo", m.id_articulo);
                    var r3 = cmdZeroSel.ExecuteReader();
                    r3.Read();
                    int nuevaCant = r3.GetInt32(0);
                    r3.Close();
                    if (nuevaCant < 0)
                    {
                        tx.Rollback();
                        throw new Exception("Cantidad inválida en carrito");
                    }

                    var cmdUpdStock = new MySqlCommand("UPDATE stock SET cantidad=cantidad+1 WHERE id_articulo=@id_articulo", conexion, tx);
                    cmdUpdStock.Parameters.AddWithValue("@id_articulo", m.id_articulo);
                    cmdUpdStock.ExecuteNonQuery();

                    if (nuevaCant == 0)
                    {
                        var cmdDel = new MySqlCommand("DELETE FROM carrito_compra WHERE id_usuario=@id_usuario AND id_articulo=@id_articulo", conexion, tx);
                        cmdDel.Parameters.AddWithValue("@id_usuario", m.id_usuario);
                        cmdDel.Parameters.AddWithValue("@id_articulo", m.id_articulo);
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