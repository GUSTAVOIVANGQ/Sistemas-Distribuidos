// Tarea 8 - Consulta de artículos por palabra clave
// (c) Alumno 2022630278. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

namespace servicio;
public class consulta_articulos
{
    class HuboError
    {
        public string mensaje;
        public HuboError(string mensaje) { this.mensaje = mensaje; }
    }
    class Articulo
    {
        public int id_articulo;
        public string? foto;
        public string nombre;
        public string descripcion;
        public decimal precio;
    }

    [Function("consulta_articulos")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        try
        {
            string? palabra_clave = req.Query["palabra_clave"];
            string? id_usuario_s = req.Query["id_usuario"];
            string? token = req.Query["token"];
            if (string.IsNullOrEmpty(palabra_clave)) throw new Exception("Se debe ingresar la palabra_clave");
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
                "SELECT s.id_articulo, fa.foto, LENGTH(fa.foto), s.nombre, s.descripcion, s.precio " +
                "FROM stock s LEFT OUTER JOIN fotos_articulos fa ON s.id_articulo = fa.id_articulo " +
                "WHERE s.nombre LIKE @like OR s.descripcion LIKE @like");
            cmd.Connection = conexion;
            cmd.Parameters.AddWithValue("@like", "%" + palabra_clave + "%");

            var r = cmd.ExecuteReader();
            try
            {
                var lista = new List<Articulo>();
                while (r.Read())
                {
                    var a = new Articulo();
                    a.id_articulo = r.GetInt32(0);
                    if (!r.IsDBNull(1))
                    {
                        int len = r.GetInt32(2);
                        byte[] foto = new byte[len];
                        r.GetBytes(1, 0, foto, 0, len);
                        a.foto = Convert.ToBase64String(foto);
                    }
                    a.nombre = r.GetString(3);
                    a.descripcion = r.GetString(4);
                    a.precio = r.GetDecimal(5);
                    lista.Add(a);
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