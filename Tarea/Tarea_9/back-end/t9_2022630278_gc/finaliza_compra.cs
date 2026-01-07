// Gestión de compras - Finalizar compra (checkout)
// Nota: GC no guarda precio localmente; consulta a GA para calcular total.
// No se persiste orden en DB (no está en el esquema de T9); se retorna un id_orden generado.
// (c) Alumno 2022630278. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System.Net.Http;

namespace servicio;
public class finaliza_compra
{
    class HuboError { public string mensaje; public HuboError(string m){ mensaje=m; } }
    class Resultado { public long id_orden; public decimal total; }
    class ArticuloGA { public int id_articulo; public string nombre=""; public decimal precio; public string? foto; }

    [Function("finaliza_compra")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
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

            // Cliente HTTP para verificar acceso en GU (una sola llamada)
            using var httpGU = new HttpClient();
            var verResp = await httpGU.GetAsync($"{USERS_URL}/api/verifica_acceso?id_usuario={id_usuario}&token={Uri.EscapeDataString(token!)}");
            if (verResp.StatusCode != System.Net.HttpStatusCode.OK) throw new Exception("Acceso denegado");

            using var conexion = new MySqlConnection($"Server={Server};UserID={UserID};Password={Password};Database={Database};SslMode=Preferred;");
            conexion.Open();

            // Leer carrito - primero obtener todos los datos
            var cmdCar = new MySqlCommand("SELECT id_articulo, cantidad FROM carrito_compra WHERE id_usuario=@u", conexion);
            cmdCar.Parameters.AddWithValue("@u", id_usuario);
            var carritoData = new List<(int id_articulo, int cantidad)>();
            using (var r = cmdCar.ExecuteReader())
            {
                while (r.Read())
                {
                    int id_articulo = r.GetInt32(0);
                    int cantidad = r.GetInt32(1);
                    carritoData.Add((id_articulo, cantidad));
                }
            }

            if (carritoData.Count == 0)
                return new BadRequestObjectResult(JsonConvert.SerializeObject(new HuboError("El carrito está vacío")));

            // Ahora hacer las llamadas HTTP fuera del reader
            var items = new List<(int id_articulo, int cantidad, decimal precio)>();
            foreach (var item in carritoData)
            {
                // Cliente HTTP independiente por llamada a GA para evitar reutilizar
                // instancias que ya han iniciado otras solicitudes.
                using var httpGA = new HttpClient();
                var resGA = await httpGA.GetAsync($"{GA_URL}/api/consulta_articulo_por_id?id_articulo={item.id_articulo}");
                if (resGA.StatusCode != System.Net.HttpStatusCode.OK)
                    throw new Exception($"Fallo obtener artículo {item.id_articulo} en GA");
                var jsonGA = await resGA.Content.ReadAsStringAsync();
                var datos = JsonConvert.DeserializeObject<ArticuloGA>(jsonGA)!;

                items.Add((item.id_articulo, item.cantidad, datos.precio));
            }

            decimal total = 0m;
            foreach (var it in items) total += it.precio * it.cantidad;

            // Vaciar carrito (no se regresa stock; ya se descontó en compra)
            using (var tx = conexion.BeginTransaction())
            {
                var cmdDel = new MySqlCommand("DELETE FROM carrito_compra WHERE id_usuario=@u", conexion, tx);
                cmdDel.Parameters.AddWithValue("@u", id_usuario);
                cmdDel.ExecuteNonQuery();
                tx.Commit();
            }

            long id_orden = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // id simulado
            var ok = new Resultado { id_orden = id_orden, total = total };
            return new OkObjectResult(JsonConvert.SerializeObject(ok));
        }
        catch (Exception e)
        {
            // Incluir tipo de excepción para diagnosticar problemas con HttpClient/URI
            var detalle = $"{e.GetType().FullName}: {e.Message}";
            return new BadRequestObjectResult(JsonConvert.SerializeObject(new HuboError(detalle)));
        }
    }
}