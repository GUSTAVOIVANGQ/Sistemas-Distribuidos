// Tarea 8 - Alta de artículo en stock
// (c) Alumno 2022630278. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

namespace servicio;
public class alta_articulo
{
    class HuboError
    {
        public string mensaje;
        public HuboError(string mensaje) { this.mensaje = mensaje; }
    }
    class Articulo
    {
        public string? nombre;
        public string? descripcion;
        public decimal? precio;
        public int? cantidad;
        public string? foto;
        public int? id_usuario;
        public string? token;
    }

    [Function("alta_articulo")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            Articulo? art = JsonConvert.DeserializeObject<Articulo>(body);
            if (art == null) throw new Exception("Se esperan los datos del artículo");
            if (string.IsNullOrEmpty(art.nombre)) throw new Exception("Se debe ingresar el nombre del artículo");
            if (string.IsNullOrEmpty(art.descripcion)) throw new Exception("Se debe ingresar la descripción del artículo");
            if (art.precio == null || art.precio <= 0) throw new Exception("El precio debe ser mayor a 0");
            if (art.cantidad == null || art.cantidad < 0) throw new Exception("La cantidad no puede ser negativa");
            if (art.id_usuario == null) throw new Exception("Se debe proporcionar id_usuario");
            if (string.IsNullOrEmpty(art.token)) throw new Exception("Se debe proporcionar token");

            string? Server = Environment.GetEnvironmentVariable("Server");
            string? UserID = Environment.GetEnvironmentVariable("UserID");
            string? Password = Environment.GetEnvironmentVariable("Password");
            string? Database = Environment.GetEnvironmentVariable("Database");
            string cs = "Server=" + Server + ";UserID=" + UserID + ";Password=" + Password + ";" + "Database=" + Database + ";SslMode=Preferred;";
            using var conexion = new MySqlConnection(cs);
            conexion.Open();

            if (!login.verifica_acceso(conexion, art.id_usuario!.Value, art.token!)) throw new Exception("Acceso denegado");

            MySqlTransaction tx = conexion.BeginTransaction();
            try
            {
                var cmd1 = new MySqlCommand();
                cmd1.Connection = conexion;
                cmd1.Transaction = tx;
                cmd1.CommandText = "INSERT INTO stock (id_articulo,nombre,descripcion,precio,cantidad) VALUES (0,@nombre,@descripcion,@precio,@cantidad)";
                cmd1.Parameters.AddWithValue("@nombre", art.nombre);
                cmd1.Parameters.AddWithValue("@descripcion", art.descripcion);
                cmd1.Parameters.AddWithValue("@precio", art.precio);
                cmd1.Parameters.AddWithValue("@cantidad", art.cantidad);
                cmd1.ExecuteNonQuery();
                long id_articulo = cmd1.LastInsertedId;

                if (art.foto != null)
                {
                    var cmd2 = new MySqlCommand();
                    cmd2.Connection = conexion;
                    cmd2.Transaction = tx;
                    cmd2.CommandText = "INSERT INTO fotos_articulos (foto,id_articulo) VALUES (@foto,@id_articulo)";
                    cmd2.Parameters.AddWithValue("@foto", Convert.FromBase64String(art.foto));
                    cmd2.Parameters.AddWithValue("@id_articulo", id_articulo);
                    cmd2.ExecuteNonQuery();
                }

                tx.Commit();
                return new OkObjectResult("{\"mensaje\":\"Se dio de alta el artículo\",\"id_articulo\":" + id_articulo + "}");
            }
            catch (Exception e)
            {
                tx.Rollback();
                throw new Exception(e.Message);
            }
        }
        catch (Exception e)
        {
            return new BadRequestObjectResult(JsonConvert.SerializeObject(new HuboError(e.Message)));
        }
    }
}