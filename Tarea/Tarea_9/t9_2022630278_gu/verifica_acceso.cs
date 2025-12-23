// (c) Carlos Pineda Guerrero. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;

namespace servicio;
public class verifica_acceso_fn
{
    class Respuesta { public bool acceso; public Respuesta(bool ok){ acceso = ok; } }
    class HuboError { public string mensaje; public HuboError(string m){ mensaje = m; } }

    [Function("verifica_acceso")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        try
        {
            string? email = req.Query["email"];
            string? token = req.Query["token"];
            string? id_usuario_s = req.Query["id_usuario"];

            string? Server = Environment.GetEnvironmentVariable("Server");
            string? UserID = Environment.GetEnvironmentVariable("UserID");
            string? Password = Environment.GetEnvironmentVariable("Password");
            string? Database = Environment.GetEnvironmentVariable("Database");
            using var conexion = new MySqlConnection($"Server={Server};UserID={UserID};Password={Password};Database={Database};SslMode=Preferred;");
            conexion.Open();

            bool ok = false;
            if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(token))
            {
                var cmd = new MySqlCommand("SELECT 1 FROM usuarios WHERE email=@e AND token=@t", conexion);
                cmd.Parameters.AddWithValue("@e", email);
                cmd.Parameters.AddWithValue("@t", token);
                using var r = cmd.ExecuteReader();
                ok = r.Read();
            }
            else if (!string.IsNullOrEmpty(id_usuario_s) && !string.IsNullOrEmpty(token))
            {
                int id_usuario = int.Parse(id_usuario_s);
                var cmd = new MySqlCommand("SELECT 1 FROM usuarios WHERE id_usuario=@id AND token=@t", conexion);
                cmd.Parameters.AddWithValue("@id", id_usuario);
                cmd.Parameters.AddWithValue("@t", token);
                using var r = cmd.ExecuteReader();
                ok = r.Read();
            }
            else
            {
                throw new Exception("Parámetros inválidos: usa (email,token) o (id_usuario,token)");
            }

            if (ok) return new OkObjectResult(JsonConvert.SerializeObject(new Respuesta(true)));
            return new BadRequestObjectResult(JsonConvert.SerializeObject(new HuboError("Acceso denegado")));
        }
        catch (Exception e)
        {
            return new BadRequestObjectResult(JsonConvert.SerializeObject(new HuboError(e.Message)));
        }
    }
}