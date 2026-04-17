namespace ParentalControl.WebService.Controllers;

public record TimeAdjustmentRequest(int MinutesAdjustment, string? Reason);
