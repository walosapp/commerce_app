using System.Text.Json.Serialization;

namespace Walos.Application.DTOs.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }

    public T? Data { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Count { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Details { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null, int? count = null)
        => new() { Success = true, Data = data, Message = message, Count = count };

    public static ApiResponse<T> Fail(string message, string? code = null, object? details = null)
        => new() { Success = false, Message = message, Code = code, Details = details };
}

public class ApiResponse : ApiResponse<object>
{
    public new static ApiResponse Ok(string? message = null)
        => new() { Success = true, Message = message };

    public new static ApiResponse Fail(string message, string? code = null, object? details = null)
        => new() { Success = false, Message = message, Code = code, Details = details };
}
