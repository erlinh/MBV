using System.Threading.Tasks;

namespace Frontend.Desktop.MessageBus
{
    /// <summary>
    /// Interface for the message bus that connects frontend to backend
    /// </summary>
    public interface IMessageBus
    {
        /// <summary>
        /// Send a command or query to the backend and get a result
        /// </summary>
        /// <typeparam name="TCommand">The type of command/query to send</typeparam>
        /// <typeparam name="TResult">The type of result expected</typeparam>
        /// <param name="command">The command/query object</param>
        /// <returns>Result from the backend</returns>
        Task<TResult> Send<TCommand, TResult>(TCommand command);
    }
} 