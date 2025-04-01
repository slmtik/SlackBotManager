using API.Interfaces;
using Slack.Interfaces;
using Slack.Models.Blocks;
using Slack.Models.Elements;
using Slack.Models.ElementStates;
using System.ComponentModel;

namespace API.VersionStrategists
{
    public abstract class VersionStrategistBase : IVersionStrategist
    {
        private static readonly Dictionary<Type, IEnumerable<(string, string, SlackElementDefinitionAttribute?)>> _valuePropertiesCache = [];
        private readonly IEnumerable<(string Name, string Description, SlackElementDefinitionAttribute? SlackElementDefinition)> _valueProperties;

        public abstract string Name { get; }
        public abstract string Description { get; }

        protected VersionStrategistBase()
        {
            var type = GetType();
            if (!_valuePropertiesCache.TryGetValue(type, out var cachedProperties))
            {
                cachedProperties = [.. type.GetProperties()
                                           .Where(x => Attribute.IsDefined(x, typeof(DescriptionAttribute)))
                                           .Select(x =>
                                           {
                                                var fieldDescription = ((DescriptionAttribute)x.GetCustomAttributes(typeof(DescriptionAttribute), true).First()).Description;
                                                return (
                                                    x.Name.ToLower(),
                                                    ((DescriptionAttribute)x.GetCustomAttributes(typeof(DescriptionAttribute), true).First()).Description,
                                                    x.GetCustomAttributes(typeof(SlackElementDefinitionAttribute), true).FirstOrDefault() as SlackElementDefinitionAttribute
                                                );
                                           })];
                _valuePropertiesCache[type] = cachedProperties;
            }

            _valueProperties = cachedProperties;
        }

        public Dictionary<string, string> ToDictionary(Dictionary<string, IElementState> elementStates)
        {
            var dict = new Dictionary<string, string>();

            foreach (var property in _valueProperties)
            {
                if (elementStates.TryGetValue(property.Name, out var elementState))
                {
                    dict[property.Name] = elementState switch
                    {
                        UrlInputState urlState => urlState.Value,
                        SelectPublicChannelState channelState => channelState.SelectedChannel!,
                        PlainTextInputState textState => textState.Value,
                        SelectUserState selectUserState => selectUserState.SelectedUser,
                        NumberInputState numberInputState => numberInputState.Value,
                        _ => string.Empty
                    };
                }
            }

            return dict;
        }

        public ICollection<IBlock> GetBlocks(Dictionary<string, string> values, Func<string, string>? blockIdNaming = null)
        {
            ICollection<IBlock> blocks = [];
            blockIdNaming ??= (string propertyName) => propertyName;

            foreach (var property in _valueProperties)
            {
                string? fieldValue = values.TryGetValue(property.Name, out var val) && !string.IsNullOrEmpty(val) 
                                    ? val 
                                    : property.SlackElementDefinition?.InitialValue;

                IInputElement inputElement = property.SlackElementDefinition?.InputElementType switch
                {
                    Type t when t == typeof(UrlInput) => new UrlInput() { InitialValue = fieldValue },
                    Type t when t == typeof(SelectPublicChannel) => new SelectPublicChannel() { InitialChannel = fieldValue },
                    Type t when t == typeof(SelectUser) => new SelectUser() { InitialUser = fieldValue },
                    Type t when t == typeof(NumberInput) => new NumberInput() { InitialValue = fieldValue },
                    _ => new PlainTextInput() { InitialValue = fieldValue }
                };

                blocks.Add(new InputBlock(property.Description, inputElement)
                {
                    BlockId = blockIdNaming(property.Name)
                });
            }

            return blocks;
        }

        public abstract Task<string> GetVersion(Dictionary<string, string> values);
    }
}
