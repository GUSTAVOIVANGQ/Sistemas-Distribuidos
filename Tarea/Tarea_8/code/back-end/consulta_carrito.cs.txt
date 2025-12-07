// Tarea 8 - Consulta del carrito de compra del usuario
// (c) Alumno 2022630278. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

namespace servicio;
public class consulta_carrito
{
    class HuboError
    {
        public string mensaje;
        public HuboError(string mensaje) { this.mensaje = mensaje; }
    }

    class ItemCarrito
    {
        public int id_articulo;
        public int cantidad;
        public string nombre;
        public decimal precio;
        public string? foto; // base64 (puede ser null)
    }

    [Function("consulta_carrito")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
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

            var cmd = new MySqlCommand(
                "SELECT cc.id_articulo, cc.cantidad, s.nombre, s.precio, fa.foto, LENGTH(fa.foto) " +
                "FROM carrito_compra cc " +
                "JOIN stock s ON s.id_articulo = cc.id_articulo " +
                "LEFT OUTER JOIN fotos_articulos fa ON fa.id_articulo = cc.id_articulo " +
                "WHERE cc.id_usuario = @id_usuario");
            cmd.Connection = conexion;
            cmd.Parameters.AddWithValue("@id_usuario", id_usuario);

            var r = cmd.ExecuteReader();
            try
            {
                var lista = new List<ItemCarrito>();
                while (r.Read())
                {
                    var item = new ItemCarrito();
                    item.id_articulo = r.GetInt32(0);
                    item.cantidad = r.GetInt32(1);
                    item.nombre = r.GetString(2);
                    item.precio = r.GetDecimal(3);
                    if (!r.IsDBNull(4))
                    {
                        int len = r.GetInt32(5);
                        byte[] foto = new byte[len];
                        r.GetBytes(4, 0, foto, 0, len);
                        item.foto = Convert.ToBase64String(foto);
                    }
                    lista.Add(item);
                }
                return new OkObjectResult(JsonConvert.SerializeObject(lista));
            }
            finally
            {
                r.Close();
            }
        }
        catch (Exception e)
        {
            return new BadRequestObjectResult(JsonConvert.SerializeObject(new HuboError(e.Message)));
        }
    }
}