namespace BrineBlade.Services.Abstractions;

public interface IRandom
{
    int Next(int minValue, int maxValue); // maxValue exclusive, like System.Random
    double NextDouble();
}
