namespace StockControl.Application.Common.Interfaces;

public record TokenResult(string AccessToken, DateTime ExpiraEn, string RefreshToken);

/// <summary>Genera los tokens JWT (access + refresh) para un usuario autenticado.</summary>
public interface IJwtTokenGenerator
{
    TokenResult Generar(string userId, string userName, IEnumerable<string> roles, IEnumerable<int> hotelesPermitidos);
}
