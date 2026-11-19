using System.Globalization;
using System.Windows.Data;

namespace Sim.Admin.Wpf.Client.Converters;

public sealed class ApprovalStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Label(value?.ToString());

    public static string Label(string? value) => value switch
    {
            "Running" => "进行中",
            "Approved" => "已通过",
            "Rejected" => "已驳回",
            "Returned" => "已退回",
            "Withdrawn" => "已撤回",
            "Terminated" => "已终止",
            "Pending" => "待处理",
            "Skipped" => "已跳过",
            "Canceled" => "已取消",
            "Draft" => "草稿",
            "Published" => "已发布",
            "Disabled" => "已停用",
            "Submit" => "提交",
            "Approve" => "同意",
            "Reject" => "驳回",
            "ReturnToPrevious" => "退回上一步",
            "ReturnToInitiator" => "退回发起人",
            "Resubmit" => "重新提交",
            "Withdraw" => "撤回",
            "Terminate" => "终止",
            "Transfer" => "转交",
            "AddSign" => "加签",
            "Urge" => "催办",
            "TimeoutNotify" => "超时提醒",
            "AutoPass" => "自动通过",
            "Cc" => "抄送",
            null => string.Empty,
            var text => text
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
