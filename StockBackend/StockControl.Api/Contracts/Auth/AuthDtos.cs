using System.ComponentModel.DataAnnotations;

namespace StockControl.Api.Contracts.Auth;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record RefreshRequest(
    [Required] string RefreshToken);

public record AuthResponse(
    string AccessToken,
    DateTime ExpiraEn,
    string RefreshToken,
    UsuarioInfo Usuario);

public record UsuarioInfo(
    string Id,
    string Nombre,
    string Email,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<int> Hoteles);

public record CrearUsuarioRequest(
    [Required] string Nombre,
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] string Rol,
    IReadOnlyCollection<int>? Hoteles);

public record UsuarioListaDto(
    string Id,
    string Nombre,
    string Email,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<int> Hoteles,
    bool Activo);

public record ActivarUsuarioRequest(bool Activo);
