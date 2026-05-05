using System.ComponentModel.DataAnnotations;

namespace Eu.EDelivery.AS4.Receivers;

public sealed class FileReceiverSettings
{
    [Required(AllowEmptyStrings = false)]
    public required string FilePath { get; set; }
    [Required(AllowEmptyStrings = false)]
    public required string FileMask { get; set; }
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = @"A batch size must be specified that > 0")]
    public required int BatchSize { get; set; }
    [Required]
    public required TimeSpan PollingInterval { get; set; }
}
