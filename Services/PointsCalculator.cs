namespace AiMarketplaceApi.Services;

public interface IPointsCalculator
{
    int CalculatePoints(int stars, int levelMultiplier);
}

public class PointsCalculator : IPointsCalculator
{
    private const int STAR_ONE_POINTS = 10;
    private const int STAR_TWO_POINTS = 20;
    private const int STAR_THREE_POINTS = 30;
    
    public int CalculatePoints(int stars, int levelMultiplier)
    {
        var basePoints = stars switch
        {
            1 => STAR_ONE_POINTS,
            2 => STAR_TWO_POINTS,
            3 => STAR_THREE_POINTS,
            _ => 0
        };
        
        return (int)(basePoints * (1 + (levelMultiplier * 0.01)));
    }
}