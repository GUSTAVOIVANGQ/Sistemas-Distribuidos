// Tarea 8 - Compra de artículo (carrito_compra + stock)
// (c) Alumno 2022630278. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

namespace servicio;
public class compra_articulo
{
    class HuboError
    {
        public string mensaje;
        public HuboError(string mensaje) { this.mensaje = mensaje; }
    }
    class Compra
    {
        public int? id_articulo;
        public int? cantidad;
        public int? id_usuario;
        public string? token;
    }

    [Function("compra_articulo")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            Compra? c = JsonConvert.DeserializeObject<Compra>(body);
            if (c == null) throw new Exception("Se esperan los datos de compra");
            if (c.id_articulo == null) throw new Exception("Se debe ingresar id_articulo");
            if (c.cantidad == null || c.cantidad <= 0) throw new Exception("La cantidad debe ser mayor a 0");
            if (c.id_usuario == null) throw new Exception("Se debe proporcionar id_usuario");
            if (string.IsNullOrEmpty(c.token)) throw new Exception("Se debe proporcionar token");

            string? Server = Environment.GetEnvironmentVariable("Server");
            string? UserID = Environment.GetEnvironmentVariable("UserID");
            string? Password = Environment.GetEnvironmentVariable("Password");
            string? Database = Environment.GetEnvironmentVariable("Database");
            string cs = "Server=" + Server + ";UserID=" + UserID + ";Password=" + Password + ";" + "Database=" + Database + ";SslMode=Preferred;";
            using var conexion = new MySqlConnection(cs);
            conexion.Open();

            if (!login.verifica_acceso(conexion, c.id_usuario!.Value, c.token!)) throw new Exception("Acceso denegado");

            // Verificar stock actual
            int existencia = 0;
            var cmd0 = new MySqlCommand("SELECT cantidad FROM stock WHERE id_articulo=@id_articulo", conexion);
            cmd0.Parameters.AddWithValue("@id_articulo", c.id_articulo);
            var r0 = cmd0.ExecuteReader();
            bool existeArt = r0.Read();
            if (existeArt) existencia = r0.GetInt32(0);
            r0.Close();

            if (!existeArt) throw new Exception("El artículo no existe");
            if (c.cantidad!.Value > existencia)
                return new BadRequestObjectResult(JsonConvert.SerializeObject(new HuboError("No hay suficientes artículos en stock")));

            // Transacción: insertar/actualizar carrito y restar stock
            var tx = conexion.BeginTransaction();
            try
            {
                // Intentar insertar, si ya existe (por índice único), actualiza
                // Primero ver si existe en el carrito
                var cmdSel = new MySqlCommand("SELECT cantidad FROM carrito_compra WHERE id_usuario=@id_usuario AND id_articulo=@id_articulo", conexion, tx);
                cmdSel.Parameters.AddWithValue("@id_usuario", c.id_usuario);
                cmdSel.Parameters.AddWithValue("@id_articulo", c.id_articulo);
                var r = cmdSel.ExecuteReader();
                bool yaExiste = r.Read();
                int cantidadActual = yaExiste ? r.GetInt32(0) : 0;
                r.Close();

                if (yaExiste)
                {
                    var cmdUpdCar = new MySqlCommand("UPDATE carrito_compra SET cantidad=@cantidad WHERE id_usuario=@id_usuario AND id_articulo=@id_articulo", conexion, tx);
                    cmdUpdCar.Parameters.AddWithValue("@cantidad", cantidadActual + c.cantidad);
                    cmdUpdCar.Parameters.AddWithValue("@id_usuario", c.id_usuario);
                    cmdUpdCar.Parameters.AddWithValue("@id_articulo", c.id_articulo);
                    cmdUpdCar.ExecuteNonQuery();
                }
                else
                {
                    var cmdInsCar = new MySqlCommand("INSERT INTO carrito_compra (id_usuario,id_articulo,cantidad) VALUES (@id_usuario,@id_articulo,@cantidad)", conexion, tx);
                    cmdInsCar.Parameters.AddWithValue("@id_usuario", c.id_usuario);
                    cmdInsCar.Parameters.AddWithValue("@id_articulo", c.id_articulo);
                    cmdInsCar.Parameters.AddWithValue("@cantidad", c.cantidad);
                    cmdInsCar.ExecuteNonQuery();
                }

                var cmdUpdStock = new MySqlCommand("UPDATE stock SET cantidad=cantidad-@cantidad WHERE id_articulo=@id_articulo", conexion, tx);
                cmdUpdStock.Parameters.AddWithValue("@cantidad", c.cantidad);
                cmdUpdStock.Parameters.AddWithValue("@id_articulo", c.id_articulo);
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