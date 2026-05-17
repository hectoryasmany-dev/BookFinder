using Asp.Versioning;
using BookFinder.Api.Contracts;
using BookFinder.Api.Mapping;
using BookFinder.Application.Common;
using BookFinder.Application.Pipeline;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

namespace BookFinder.Api.Controllers;

/// <summary>Book search endpoints.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/books")]
public sealed class BooksController : ControllerBase
{
    private readonly ISearchPipeline _pipeline;
    private readonly IValidator<SearchBookRequest> _validator;

    public BooksController(ISearchPipeline pipeline, IValidator<SearchBookRequest> validator)
    {
        _pipeline = pipeline;
        _validator = validator;
    }

    /// <summary>
    /// Search for books using a free-text query.
    /// </summary>
    /// <remarks>
    /// Sample requests:
    ///
    ///     POST /api/v1/books/search
    ///     { "query": "tolkien hobbit illustrated deluxe 1937" }
    ///
    ///     POST /api/v1/books/search
    ///     { "query": "dickens tale two cities" }
    ///
    ///     POST /api/v1/books/search
    ///     { "query": "mark huckleberry" }
    /// </remarks>
    /// <param name="request">The search request containing the raw query string.</param>
    /// <param name="cancellationToken">Propagated from the HTTP request lifecycle.</param>
    /// <response code="200">Ranked list of up to 5 book candidates.</response>
    /// <response code="400">Query failed validation.</response>
    /// <response code="429">Rate limit exceeded.</response>
    /// <response code="502">Open Library is unreachable.</response>
    [HttpPost("search")]
    [EnableRateLimiting("search")]
    [OutputCache(PolicyName = "SearchCache")]
    [ProducesResponseType(typeof(SearchBookResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Search(
        [FromBody] SearchBookRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return ValidationProblem(validation);

        Result<Application.Models.SearchBookResponse> result;
        try
        {
            result = await _pipeline.SearchAsync(request.Query, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499);
        }

        if (result.IsSuccess)
            return Ok(SearchResultMapper.ToDto(result.Value!));

        return result.Error!.Code switch
        {
            "openlibrary.unavailable" => Problem(
                detail: result.Error.Message,
                statusCode: StatusCodes.Status502BadGateway,
                title: "Upstream unavailable"),
            _ => Problem(
                detail: result.Error.Message,
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private IActionResult ValidationProblem(FluentValidation.Results.ValidationResult validation)
    {
        var details = new ValidationProblemDetails(
            validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()))
        {
            Status = StatusCodes.Status400BadRequest
        };
        return BadRequest(details);
    }
}
