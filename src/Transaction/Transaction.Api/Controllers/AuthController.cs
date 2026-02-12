using BuildingBlocks.Contracts.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Transaction.Application.Abstractions;
using Transaction.Application.Users;

namespace Transaction.Api.Controllers;

/// <summary>
/// Authentication endpoints for user management
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class AuthController(
    ISender mediator,
    IUserRepository userRepository,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>
    /// Register new user account
    /// </summary>
    /// <param name="request">SignUp request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created user ID</returns>
    /// <response code="201">User created successfully</response>
    /// <response code="400">Invalid request or email already exists</response>
    /// <response code="429">Too many requests - rate limit exceeded</response>
    [HttpPost("signup")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult> SignUp(
        [FromBody] SignUpRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SignUpCommand(
            request.Email,
            request.Password,
            request.FullName);

        var userId = await mediator.Send(command, cancellationToken);

        logger.LogInformation("User signed up successfully | UserId={UserId}", userId);

        return CreatedAtAction(
            nameof(GetCurrentUser),
            new { },
            new { userId, email = request.Email });
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JWT Token and user info</returns>
    /// <response code="200">Login successful</response>
    /// <response code="401">Invalid credentials</response>
    /// <response code="429">Too many requests - rate limit exceeded</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResult>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await mediator.Send(command, cancellationToken);

        logger.LogInformation("User logged in | UserId={UserId}", result.UserId);

        return Ok(result);
    }

    /// <summary>
    /// Deactivate user account (soft delete)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    /// <response code="204">User deactivated successfully</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="404">User not found</response>
    [HttpDelete("me")]
    [Authorize]
    public async Task<ActionResult> DeactivateAccount(
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var command = new DeactivateUserCommand(userId, "User requested account deletion");
        await mediator.Send(command, cancellationToken);

        logger.LogInformation("User deactivated account | UserId={UserId}", userId);

        return NoContent();
    }

    /// <summary>
    /// Get current authenticated user info
    /// </summary>
    /// <returns>Current user info from JWT claims</returns>
    /// <response code="200">User info retrieved</response>
    /// <response code="401">Unauthorized</response>
    [HttpGet("me")]
    [Authorize]
    public ActionResult GetCurrentUser()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        return Ok(new
        {
            userId,
            email,
            role
        });
    }

    /// <summary>
    /// Get all users with pagination (Admin only)
    /// </summary>
    /// <param name="request">Pagination request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of users</returns>
    /// <response code="200">Users retrieved successfully</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden - Admin role required</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("users")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<PagedResponse<object>>> GetAllUsers(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = request.Normalize();

        var (items, totalCount) = await userRepository.GetAllPagedAsync(
            normalized.Skip,
            normalized.PageSize,
            normalized.SortBy,
            normalized.SortDirection,
            cancellationToken);

        var dtos = items.Select(u => new
        {
            u.Id,
            u.Email,
            u.FullName,
            u.Role,
            u.Status,
            u.IsDeleted,
            u.CreatedAtUtc,
            u.UpdatedAtUtc,
            u.LastLoginAtUtc
        }).ToList();

        var response = new PagedResponse<object>(
            dtos,
            normalized.Page,
            normalized.PageSize,
            totalCount);

        logger.LogInformation(
            "Users retrieved | Page={Page} PageSize={PageSize} TotalCount={TotalCount}",
            normalized.Page,
            normalized.PageSize,
            totalCount);

        return Ok(response);
    }
}

public sealed record SignUpRequest(string Email, string Password, string FullName);
public sealed record LoginRequest(string Email, string Password);
