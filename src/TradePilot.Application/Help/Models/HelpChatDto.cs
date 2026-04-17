namespace TradePilot.Application.Help.Models;

public sealed record HelpChatRequestDto(string Question);

public sealed record HelpChatResponseDto(string Answer);
