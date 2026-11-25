using System.Security;
using YEAPI.Models;
using YEAPI.Services;

namespace YEAPI.RK7;

public static class RK7Functions
{
    public static RK7Reference GetMenu(
        RK7Service service,
        RK7Connection connection)
    {
        string request = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<RK7Query>
<RK7CMD
CMD=""GetRefData""
RefName=""CATEGLIST""
OnlyActive=""true""
WithMacroProp=""1""
MacroPropTags=""0""
WithChildItems=""3""
PropMask=""RIChildItems.(Code, Name, RIChildItems.Code, RIChildItems.Name, RIChildItems.PRICETYPES*, RIChildItems.(Code, Name, RIChildItems.Code, RIChildItems.Name, RIChildItems.PRICETYPES*, RIChildItems.(Code, Name, RIChildItems.Code, RIChildItems.Name, RIChildItems.PRICETYPES*)))"">
</RK7CMD>
</RK7Query>";

        string xml = service.SendXml(
            connection,
            request);

        return RK7Parser.Parse(xml);
    }


    public static RK7Reference GetAllMenu(
        RK7Service service,
        RK7Connection connection)
    {
        string request = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<RK7Query>
<RK7CMD
CMD=""GetRefData""
RefName=""CATEGLIST""
OnlyActive=""true""
WithMacroProp=""1""
MacroPropTags=""0""
WithChildItems=""3""
PropMask=""RIChildItems.(Code, Name, RIChildItems.Code, RIChildItems.Name, RIChildItems.PRICETYPES*, RIChildItems.(Code, Name, RIChildItems.Code, RIChildItems.Name, RIChildItems.PRICETYPES*, RIChildItems.(Code, Name, RIChildItems.Code, RIChildItems.Name, RIChildItems.PRICETYPES*)))"">
</RK7CMD>
</RK7Query>";

        string xml = service.SendXml(
            connection,
            request);

        return RK7Parser.ParseAll(xml);
    }

    // ============================================================
    // Отправка сообщения на кассу RK7
    // ============================================================

    public static string SendWaiterMessage(
        RK7Service service,
        RK7Connection connection,
        string messageText,
        int expireTime)
    {
        string safeMessage = SecurityElement.Escape(messageText) ?? string.Empty;

        string request =
            $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<RK7Query>
    <RK7CMD
        CMD=""WaiterMessage""
        text=""{safeMessage}""
        expireTime=""{expireTime}""
        messageType=""XMLInterface"">
    </RK7CMD>
</RK7Query>";

        return service.SendXml(connection, request);
    }

    public static string GetSystemInfo2(
    RK7Service service,
    RK7Connection connection)
    {
        string request = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<RK7Query>
    <RK7CMD CMD=""GetSystemInfo2""></RK7CMD>
</RK7Query>";

        return service.SendXml(
            connection,
            request);
    }

    public static string SetDishRest(
    RK7Service service,
    RK7Connection connection,
    int code,
    bool prohibited)
    {
        string request =
            $@"<?xml version=""1.0"" encoding=""utf-8""?>
<RK7Query>
  <RK7Command CMD=""SetDishRests"">
    <DishRest code=""{code}"" prohibited=""{prohibited.ToString().ToLowerInvariant()}"" />
  </RK7Command>
</RK7Query>";

        return service.SendXml(
            connection,
            request);
    }

}
