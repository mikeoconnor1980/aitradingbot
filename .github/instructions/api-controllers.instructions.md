---
applyTo: "**/Controllers/**/*.cs"
---

# API Controller Patterns

## Controller Structure

All API controllers follow these conventions:

```csharp
[ApiController]
[Route("api/my-feature")]
[Produces("application/json")]
public sealed class MyFeatureController : ApiController
{
    public MyFeatureController(IMediator mediator, IdentityService identityService) 
        : base(mediator, identityService)
    {
    }

    [HttpGet("{workareaId}/items")]
    [ProducesResponseType(typeof(List<ItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetItems(Guid workareaId)
    {
        var items = await Mediator.Send(new GetItemsQuery(workareaId, IdentityService.Identity));
        return Ok(items);
    }

    [HttpPost("{workareaId}/items")]
    [ProducesResponseType(typeof(CreatedResultEnvelope), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateItem(Guid workareaId, [FromBody] ItemCreateDto item)
    {
        var itemId = await Mediator.Send(new CreateItemCommand(workareaId, item, IdentityService.Identity));
        return CreatedAtAction(nameof(GetItems), new { workareaId }, new CreatedResultEnvelope(itemId));
    }

    [HttpPut("{workareaId}/items/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateItem(Guid workareaId, Guid id, [FromBody] ItemUpdateDto item)
    {
        await Mediator.Send(new UpdateItemCommand(id, item, IdentityService.Identity));
        return NoContent();
    }

    [HttpDelete("{workareaId}/items/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteItem(Guid workareaId, Guid id)
    {
        await Mediator.Send(new DeleteItemCommand(workareaId, id, IdentityService.Identity));
        return NoContent();
    }
}
```

## URL Conventions

- Use kebab-case for route segments: `api/entity-management`
- Use plural nouns for collections: `items`, `permissions`
- Use verbs only for actions: `api/reports/{id}/generate`

## HTTP Status Codes

| Action | Success | Common Errors |
|--------|---------|---------------|
| GET (single) | 200 OK | 404 Not Found |
| GET (list) | 200 OK | 404 Not Found |
| POST (create) | 201 Created | 400 Bad Request |
| PUT (update) | 204 No Content | 400, 404 |
| DELETE | 204 No Content | 404 Not Found |

## Exception Handling

<!-- DOT-CUSTOM-START:api-exceptions -->
Global exception handling is in [HttpGlobalExceptionFilter.cs](../../src/DTS.UKCT.Efiling.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs):

| Exception Type | HTTP Status |
|----------------|-------------|
| `DomainException` | 400 Bad Request |
| `NotFoundException` | 404 Not Found |
| `UnauthorizedAccessException` | 403 Forbidden |
| `DbUpdateConcurrencyException` | 412 Precondition Failed |
| Other exceptions | 500 Internal Server Error |
<!-- DOT-CUSTOM-END:api-exceptions -->

## File Downloads

```csharp
[HttpGet("{workareaId}/items/report")]
[ProducesResponseType(typeof(File), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetReport(Guid workareaId, [FromQuery] string languageCode)
{
    var report = await Mediator.Send(new GetReportQuery(workareaId, languageCode, IdentityService.Identity));
    return File(report.Report, "application/octet-stream", report.FileName);
}
```

## File Uploads

```csharp
[HttpPost("{workareaId}/import")]
[ProducesResponseType(typeof(CreatedResultEnvelope), StatusCodes.Status201Created)]
[RequestSizeLimit(MaxRequestBodySizeBytes)]
[RequestFormLimits(ValueLengthLimit = MaxRequestBodySizeBytes, MultipartBodyLengthLimit = MaxRequestBodySizeBytes)]
public async Task<IActionResult> Import(Guid workareaId, IFormFile file)
{
    await Mediator.Send(new ImportCommand(workareaId, file.OpenReadStream(), file.FileName, IdentityService.Identity));
    return CreatedAtAction(nameof(GetImportStatus), new { workareaId }, new CreatedResultEnvelope(workareaId));
}
```
