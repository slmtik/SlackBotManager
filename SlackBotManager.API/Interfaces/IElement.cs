﻿using System.Text.Json.Serialization;
using SlackBotManager.API.Models.Elements;

namespace SlackBotManager.API.Interfaces
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(PlainText), typeDiscriminator: "plain_text")]
    [JsonDerivedType(typeof(MarkdownText), typeDiscriminator: "mrkdwn")]
    [JsonDerivedType(typeof(UrlInput), typeDiscriminator: "url_text_input")]
    [JsonDerivedType(typeof(MultiStaticSelect), typeDiscriminator: "multi_static_select")]
    [JsonDerivedType(typeof(Button), typeDiscriminator: "button")]
    [JsonDerivedType(typeof(NumberInput), typeDiscriminator: "number_input")]
    [JsonDerivedType(typeof(Image), typeDiscriminator: "image")]
    [JsonDerivedType(typeof(SelectPublicChannel), typeDiscriminator: "channels_select")]
    [JsonDerivedType(typeof(SelectUser), typeDiscriminator: "users_select")]
    [JsonDerivedType(typeof(MultiSelectUser), typeDiscriminator: "multi_users_select")]
    [JsonDerivedType(typeof(MultiSelectConversations), typeDiscriminator: "multi_conversations_select")]
    [JsonDerivedType(typeof(PlainTextInput), typeDiscriminator: "plain_text_input")]
    public interface IElement
    {
    }
}
