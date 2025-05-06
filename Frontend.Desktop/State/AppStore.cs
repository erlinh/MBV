using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Frontend.Desktop.State
{
    /// <summary>
    /// A Zustand-style store for application state
    /// </summary>
    public class AppStore
    {
        // Event for notifying subscribers of state changes
        public event Action? StateChanged;

        // Example state: notes
        private readonly List<NoteDto> _notes = new();
        public IReadOnlyList<NoteDto> Notes => _notes;

        // Example state: current user
        private UserDto? _currentUser;
        public UserDto? CurrentUser => _currentUser;

        // Example state: UI state
        public bool IsSidebarOpen { get; private set; } = true;
        public string CurrentView { get; private set; } = "home";

        // Message bus for sending commands and queries
        private readonly MessageBus.IMessageBus _bus;

        public AppStore(MessageBus.IMessageBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        // Notify subscribers of state changes
        private void Notify()
        {
            StateChanged?.Invoke();
        }

        // Example method: Add a note
        public async Task AddNote(string title, string content)
        {
            try
            {
                // This would use real commands defined in the Shared project
                var result = await _bus.Send<dynamic, dynamic>(new
                {
                    Type = "SaveNoteCommand",
                    Title = title,
                    Content = content
                });

                if (result?.Success == true && result.Id != null)
                {
                    _notes.Add(new NoteDto(
                        Guid.Parse(result.Id.ToString()),
                        title,
                        content,
                        DateTime.Now
                    ));
                }
                
                Notify();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to add note: {ex.Message}");
                throw;
            }
        }

        // Example method: Delete a note
        public async Task DeleteNote(Guid id)
        {
            try
            {
                var result = await _bus.Send<dynamic, dynamic>(new
                {
                    Type = "DeleteNoteCommand",
                    Id = id
                });

                if (result?.Success == true)
                {
                    var noteToRemove = _notes.Find(n => n.Id == id);
                    if (noteToRemove != null)
                    {
                        _notes.Remove(noteToRemove);
                    }
                }

                Notify();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete note: {ex.Message}");
                throw;
            }
        }

        // Example method: Load user
        public async Task LoadCurrentUser()
        {
            try
            {
                var result = await _bus.Send<dynamic, dynamic>(new
                {
                    Type = "GetCurrentUserQuery"
                });

                if (result != null)
                {
                    _currentUser = new UserDto(
                        result.Id.ToString(),
                        result.Username.ToString(),
                        result.Email.ToString()
                    );
                }

                Notify();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load current user: {ex.Message}");
                throw;
            }
        }

        // UI State methods
        public void ToggleSidebar()
        {
            IsSidebarOpen = !IsSidebarOpen;
            Notify();
        }

        public void NavigateTo(string view)
        {
            CurrentView = view;
            Notify();
        }
    }

    // DTOs for the store
    public record NoteDto(Guid Id, string Title, string Content, DateTime CreatedAt);
    public record UserDto(string Id, string Username, string Email);
} 