// Gestión de compras - Consulta del carrito
// Enriquecimiento de nombre/precio/foto vía GA (no se leen de DB local)
// (c) Alumno 2022630278. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System.Net.Http;

namespace servicio;
public class consulta_carrito
{
    class HuboError { public string mensaje; public HuboError(string m){ mensaje=m; } }
    class ItemCarrito { public int id_articulo; public int cantidad; public string nombre=""; public decimal precio; public string? foto; }
    class ArticuloGA { public int id_articulo; public string nombre=""; public decimal precio; public string? foto; }

    [Function("consulta_carrito")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
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
            string? USERS_URL = Environment.GetEnvironmentVariable("USERS_URL");
            string? GA_URL = Environment.GetEnvironmentVariable("GA_URL");

            using var http = new HttpClient();
            var verResp = await http.GetAsync($"{USERS_URL}/api/verifica_acceso?id_usuario={id_usuario}&token={Uri.EscapeDataString(token!)}");
            if (verResp.StatusCode != System.Net.HttpStatusCode.OK) throw new Exception("Acceso denegado");

            using var conexion = new MySqlConnection($"Server={Server};UserID={UserID};Password={Password};Database={Database};SslMode=Preferred;");
            conexion.Open();

            var cmd = new MySqlCommand("SELECT id_articulo, cantidad FROM carrito_compra WHERE id_usuario=@u", conexion);
            cmd.Parameters.AddWithValue("@u", id_usuario);

            // Primero leer todos los datos del carrito
            var carritoData = new List<(int id_articulo, int cantidad)>();
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    int id_articulo = r.GetInt32(0);
                    int cantidad = r.GetInt32(1);
                    carritoData.Add((id_articulo, cantidad));
                }
            }

            // Ahora hacer las llamadas HTTP fuera del reader
            var lista = new List<ItemCarrito>();
            foreach (var item in carritoData)
            {
                var resGA = await http.GetAsync($"{GA_URL}/api/consulta_articulo_por_id?id_articulo={item.id_articulo}");
                if (resGA.StatusCode != System.Net.HttpStatusCode.OK)
                    throw new Exception($"Fallo obtener artículo {item.id_articulo} en GA");

                var jsonGA = await resGA.Content.ReadAsStringAsync();
                var datos = JsonConvert.DeserializeObject<ArticuloGA>(jsonGA)!;

                lista.Add(new ItemCarrito {
                    id_articulo = item.id_articulo,
                    cantidad = item.cantidad,
                    nombre = datos.nombre,
                    precio = datos.precio,
                    foto = datos.foto
                });
            }

            return new OkObjectResult(JsonConvert.SerializeObject(lista));
        }
        catch (Exception e)
        {
            // Incluir tipo de excepción para diagnosticar problemas con HttpClient/URI
            var detalle = $"{e.GetType().FullName}: {e.Message}";
            return new BadRequestObjectResult(JsonConvert.SerializeObject(new HuboError(detalle)));
        }
    }
}