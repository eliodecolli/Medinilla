using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DataEnum
{
    @string,
    @decimal,
    integer,
    dateTime,
    boolean,
    OptionList,
    SequenceList,
    MemberList,
    passwordString
}
