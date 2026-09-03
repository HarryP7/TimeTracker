namespace TimeTracker.Services
{
    public static class WorkTimeCalculator
    {
        /// <summary>
        /// Hасчет: Время начала + 9 часов + паузы - 1 час (если был обед)
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="totalPauseSeconds"></param>
        /// <param name="isLunchIncluded"></param>
        public static DateTime CalculateEstimatedEndTime(DateTime startTime, int totalPauseSeconds, bool isLunchIncluded)
        {
            var result = startTime.ToLocalTime()
                .AddHours(9)
                .AddSeconds(totalPauseSeconds);

            // Переключатель "Был ли обед" (Если включен — вычитаем 1 час из итогового времени нахождения на работе)
            if (isLunchIncluded)
            {
                result = result.AddHours(-1);
            }

            return result;
        }
    }

}
