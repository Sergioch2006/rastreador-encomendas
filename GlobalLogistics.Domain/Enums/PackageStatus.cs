namespace GlobalLogistics.Domain.Enums;

public enum PackageStatus
{
    Created = 0,
    InTransit = 1,
    OutForDelivery = 2,
    Delivered = 3,
    Exception = 4,
    Returned = 5
}
