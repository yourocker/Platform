namespace MedicalBot.Entities.Tasks
{
    /// <summary>
    /// Статусы жизненного цикла задачи
    /// </summary>
    public enum TaskStatus
    {
        /// <summary>
        /// Задача поставлена (Новая)
        /// </summary>
        Created = 0,

        /// <summary>
        /// Исполнитель принял задачу и работает над ней
        /// </summary>
        InProgress = 1,

        /// <summary>
        /// Задача на проверке у постановщика (Приемка)
        /// </summary>
        InReview = 2,

        /// <summary>
        /// Задача успешно завершена
        /// </summary>
        Completed = 3,
    }
}