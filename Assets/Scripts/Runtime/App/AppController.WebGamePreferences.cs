using UnityEngine;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        // This transient value schedules ACK retries; SaveData owns the durable
        // import identity, shared by both web games and retained across restarts.
        private string webGameImportedPreferenceRevision;

        private void RestoreWebGamePendingPreferences()
        {
            if (store == null || game == null || data == null || !WebGameFeedbackPreferences.TryRead(out var pending)) return;
            if (data.ImportWebGameFeedback(pending.revision,pending.soundEnabled,pending.hapticsEnabled))
            {
                if (!data.SoundEnabled) feedback?.Stop();
            }
            webGameImportedPreferenceRevision = pending.revision;
            // Keep the native journal until its revision is durable in the host save.
            SaveHostData();
        }

        private void AcknowledgeWebGameImportedPreferences()
        {
            if (string.IsNullOrEmpty(webGameImportedPreferenceRevision)) return;
            // Native compares the revision so an old ACK cannot consume a newer choice.
            if (WebGameFeedbackPreferences.Acknowledge(webGameImportedPreferenceRevision)) webGameImportedPreferenceRevision = null;
            else Debug.LogWarning("Web game preferences were saved; native acknowledgement will retry on the next recovery.");
        }
    }
}
