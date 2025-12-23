using System.Net.Http;
using System.Threading.Tasks;

namespace servicio;
public static class AccessClient
{
    private static readonly HttpClient httpClient;
    private static readonly string? usersBaseUrl;

    static AccessClient()
    {
        usersBaseUrl = Environment.GetEnvironmentVariable("USERS_URL");
        httpClient = new HttpClient();
        if (!string.IsNullOrEmpty(usersBaseUrl))
        {
            // Configura BaseAddress una sola vez; NO modifiques propiedades luego.
            httpClient.BaseAddress = new Uri(usersBaseUrl);
        }
        // Ejemplos de cosas que NO debes hacer después de usar el cliente:
        // - Cambiar Timeout, DefaultRequestVersion/RequestVersion, Proxy/Handler, etc.
    }

    // Verifica acceso por id_usuario + token llamando al servicio de usuarios (GU)
    public static async Task<bool> VerificaAccesoPorIdAsync(int id_usuario, string token)
    {
        if (string.IsNullOrEmpty(usersBaseUrl))
            throw new Exception("USERS_URL no está definido en el entorno");

        // Usa un RequestMessage separado por llamada si necesitas headers específicos.
        var url = $"/api/verifica_acceso?id_usuario={Uri.EscapeDataString(id_usuario.ToString())}&token={Uri.EscapeDataString(token)}";
        using var resp = await httpClient.GetAsync(url);
        return resp.IsSuccessStatusCode; // 200 válido, 400/401 inválido según tu implementación en GU
    }

    // Opcional: verificación por email + token
    public static async Task<bool> VerificaAccesoPorEmailAsync(string email, string token)
    {
        if (string.IsNullOrEmpty(usersBaseUrl))
            throw new Exception("USERS_URL no está definido en el entorno");

        var url = $"/api/verifica_acceso?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
        using var resp = await httpClient.GetAsync(url);
        return resp.IsSuccessStatusCode;
    }
}