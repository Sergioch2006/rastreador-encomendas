using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlobalLogistics.AuthAPI.Models;
using GlobalLogistics.AuthAPI.Models.DTOs;
using GlobalLogistics.AuthAPI.Services;
using System.Security.Claims;

namespace GlobalLogistics.AuthAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// Registra um novo usuário no sistema
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Verifica se usuário já existe
        if (await _userManager.FindByEmailAsync(request.Email) != null)
            return BadRequest(new { message = "Email já cadastrado" });

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            EmailConfirmed = true // Em produção: enviar email de confirmação
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        
        if (!result.Succeeded)
            return BadRequest(new { message = "Erro ao criar usuário", errors = result.Errors });

        // Atribui role padrão
        await _userManager.AddToRoleAsync(user, "User");

        var token = _tokenService.GenerateJwtToken(user, await _userManager.GetRolesAsync(user));
        var refreshToken = _tokenService.GenerateRefreshToken();

        _logger.LogInformation("Usuário registrado: {Email}", request.Email);

        return CreatedAtAction(nameof(Register), new
        {
            token,
            refreshToken,
            expiration = DateTime.UtcNow.AddMinutes(60),
            user = new UserInfo
            {
                Id = user.Id,
                Email = user.Email!,
                FullName = user.FullName
            }
        });
    }

    /// <summary>
    /// Autentica usuário e retorna token JWT
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(request.Email);
        
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            _logger.LogWarning("Tentativa de login falhou para: {Email}", request.Email);
            return Unauthorized(new { message = "Credenciais inválidas" });
        }

        if (!user.IsActive)
            return Unauthorized(new { message = "Conta desativada" });

        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenService.GenerateJwtToken(user, roles);
        var refreshToken = _tokenService.GenerateRefreshToken();

        _logger.LogInformation("Login bem-sucedido: {Email}", request.Email);

        return Ok(new
        {
            token,
            refreshToken,
            expiration = DateTime.UtcNow.AddMinutes(60),
            user = new UserInfo
            {
                Id = user.Id,
                Email = user.Email!,
                FullName = user.FullName
            }
        });
    }

    /// <summary>
    /// Renova o token usando refresh token
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        // Implementação simplificada - em produção, valide refreshToken no banco
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Unauthorized();

        var principal = _tokenService.GetPrincipalFromExpiredToken(request.Token);
        if (principal?.Identity?.Name == null)
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (user == null || !user.IsActive)
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        var newToken = _tokenService.GenerateJwtToken(user, roles);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        return Ok(new
        {
            token = newToken,
            refreshToken = newRefreshToken,
            expiration = DateTime.UtcNow.AddMinutes(60)
        });
    }
}

// DTO para refresh
public class RefreshRequest
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}
