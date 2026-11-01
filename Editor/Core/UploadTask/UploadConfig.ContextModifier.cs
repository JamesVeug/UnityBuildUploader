namespace Wireframe
{
    public partial class UploadConfig
    {
        private class UploadConfigContext : Context
        {
            private UploadConfig config;

            public UploadConfigContext(UploadConfig config)
            {
                this.config = config;
            }

            public override bool TryFormatKeyLocally(string key, out string value)
            {
                if (base.TryFormatKeyLocally(key, out value))
                {
                    return true;
                }

                foreach (SourceData source in config.Sources)
                {
                    if (!source.Enabled || source.Source == null)
                    {
                        continue;
                    }

                    if (source.Source.Context.TryFormatKeyLocally(key, out value))
                    {
                        return true;
                    }
                }

                foreach (ModifierData modifier in config.Modifiers)
                {
                    if (!modifier.Enabled || modifier.Modifier == null)
                    {
                        continue;
                    }

                    if (modifier.Modifier.Context.TryFormatKeyLocally(key, out value))
                    {
                        return true;
                    }
                }

                foreach (DestinationData destination in config.Destinations)
                {
                    if (!destination.Enabled || destination.Destination == null)
                    {
                        continue;
                    }

                    if (destination.Destination.Context.TryFormatKeyLocally(key, out value))
                    {
                        return true;
                    }
                }

                value = "";
                return false;
            }
        }

        /// <summary>
        /// Parents this config's contexts without caching anything. A resumed task restores its cached values
        /// instead, because caching again would bump the build number.
        /// </summary>
        internal void SetContext(Context context)
        {
            m_context.SetParent(context);

            foreach (UploadActionData actionData in m_postActions)
            {
                if (actionData.WhenToExecute != UploadActionData.UploadCompleteStatus.Never && actionData.UploadAction != null)
                {
                    actionData.UploadAction.Context.SetParent(m_context);
                }
            }
        }

        public void SetContextAndCacheCallbacks(Context context)
        {
            SetContext(context);

            foreach (SourceData sourceData in m_buildSources)
            {
                if (sourceData.Enabled && sourceData.Source != null)
                {
                    sourceData.Source.PrepareContextForCaching();
                    sourceData.Source.Context.CacheCallbacks();
                }
            }

            foreach (ModifierData modifierData in m_modifiers)
            {
                if (modifierData.Enabled && modifierData.Modifier != null)
                {
                    modifierData.Modifier.Context.CacheCallbacks();
                }
            }

            foreach (DestinationData destinationData in m_buildDestinations)
            {
                if (destinationData.Enabled && destinationData.Destination != null)
                {
                    destinationData.Destination.Context.CacheCallbacks();
                }
            }

            foreach (UploadActionData actionData in m_postActions)
            {
                if (actionData.WhenToExecute != UploadActionData.UploadCompleteStatus.Never && actionData.UploadAction != null)
                {
                    actionData.UploadAction.Context.CacheCallbacks();
                }
            }
            
            m_context.CacheCallbacks();
        }
    }
}