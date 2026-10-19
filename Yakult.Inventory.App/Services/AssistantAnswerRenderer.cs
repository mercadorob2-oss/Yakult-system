using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Yakult.Inventory.App.Services
{
    /// <summary>Native controls only; untrusted reply text is never interpreted as XAML or HTML.</summary>
    internal static class AssistantAnswerRenderer
    {
        internal const double MessageSize=13.0*96/72;
        private static readonly Brush Ink=new SolidColorBrush(Color.FromRgb(32,38,53));
        private static readonly Brush Muted=new SolidColorBrush(Color.FromRgb(80,91,108));
        internal static StackPanel Render(AssistantPresentation answer,Action<AssistantFollowUp> followUp,Style buttonStyle,Action<string> copyText=null)
        {
            var panel=new StackPanel();
            var heading=Text(answer.Headline,MessageSize,Ink);heading.FontWeight=FontWeights.SemiBold;panel.Children.Add(heading);
            if(answer.Filters.Count>0) panel.Children.Add(Text(string.Join(" · ",answer.Filters),13,Muted,new Thickness(0,5,0,0)));
            foreach(string qualification in answer.Qualifications) panel.Children.Add(Text(qualification,14,Ink,new Thickness(0,6,0,0)));
            AddProse(panel,answer.Body);
            if(answer.Metrics.Count>0)
            {
                var metrics=new StackPanel {Margin=new Thickness(0,8,0,2)};
                foreach(var field in answer.Metrics) metrics.Children.Add(Field(field));
                panel.Children.Add(metrics);
            }
            for(int i=0;i<answer.Children.Count;i++)
            {
                var group=new StackPanel();group.Children.Add(Text("Result "+(i+1),13,Muted,new Thickness(0,0,0,5)));
                group.Children.Add(Render(answer.Children[i],followUp,buttonStyle,copyText));
                panel.Children.Add(Surface(group));
            }
            if(answer.PageCaption!=null) panel.Children.Add(Text(answer.PageCaption,13,Muted,new Thickness(0,8,0,3)));
            var records=new StackPanel();
            foreach(var record in answer.Records.Take(AssistantPresentationFormatter.PreviewCount)) records.Children.Add(Record(record));
            panel.Children.Add(records);
            if(answer.Records.Count>AssistantPresentationFormatter.PreviewCount)
            {
                bool expanded=false;
                var more=ActionButton("Show more ("+(answer.Records.Count-AssistantPresentationFormatter.PreviewCount)+")",buttonStyle);
                more.ToolTip="Reveal records already loaded on this page";
                more.Click+=(_,__)=>
                {
                    expanded=!expanded;
                    if(expanded) foreach(var record in answer.Records.Skip(AssistantPresentationFormatter.PreviewCount)) records.Children.Add(Record(record));
                    else while(records.Children.Count>AssistantPresentationFormatter.PreviewCount) records.Children.RemoveAt(records.Children.Count-1);
                    more.Content=expanded?"Show fewer":"Show more ("+(answer.Records.Count-AssistantPresentationFormatter.PreviewCount)+")";
                };
                panel.Children.Add(more);
            }
            AddProse(panel,answer.Explanation);
            var actions=new WrapPanel {Margin=new Thickness(0,8,0,0)};
            var copy=ActionButton("Copy",buttonStyle);
            copy.Click+=(_,__)=>
            {
                try {if(copyText==null) Clipboard.SetText(answer.CopyText);else copyText(answer.CopyText);copy.Content="Copied";}
                catch(System.Runtime.InteropServices.COMException) {copy.Content="Retry copy";}
            };
            actions.Children.Add(copy);
            foreach(var action in answer.Actions)
            {
                var button=ActionButton(action.Label,buttonStyle);
                button.Click+=(_,__)=>
                {
                    if(action.Choice!=null)
                        foreach(var choiceButton in actions.Children.OfType<Button>().Where(b=>b.Tag is AssistantClarificationChoice)) choiceButton.IsEnabled=false;
                    followUp(action);
                };
                if(action.Choice!=null) button.Tag=action.Choice;
                actions.Children.Add(button);
            }
            panel.Children.Add(actions);
            if(answer.Details.Count>0)
            {
                var details=new StackPanel {Margin=new Thickness(0,5,0,0)};
                foreach(var field in answer.Details) details.Children.Add(Field(field));
                panel.Children.Add(new Expander {Header="Details",FontSize=13,Foreground=Muted,IsExpanded=false,Content=details,Margin=new Thickness(0,3,0,0)});
            }
            return panel;
        }
        private static Border Record(AssistantRecordPreview record)
        {
            var body=new StackPanel();var title=Text(record.Title,15,Ink);title.FontWeight=FontWeights.SemiBold;body.Children.Add(title);
            int shown=record.IsDetail?record.Fields.Count:3;
            foreach(var field in record.Fields.Take(shown)) body.Children.Add(Field(field));
            if(record.Fields.Count>shown)
            {
                var rest=new StackPanel();foreach(var field in record.Fields.Skip(shown)) rest.Children.Add(Field(field));
                body.Children.Add(new Expander {Header="Record fields",FontSize=13,Foreground=Muted,Content=rest,Margin=new Thickness(0,4,0,0)});
            }
            return Surface(body);
        }
        private static Border Surface(UIElement child)
        {return new Border {Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(222,226,232)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(7),Padding=new Thickness(8),Margin=new Thickness(0,5,0,1),Child=child};}
        private static TextBlock Field(AssistantField field)
        {
            var block=Text("",14,Ink,new Thickness(0,3,0,0));
            block.Inlines.Add(new Run(field.Label+": ") {Foreground=Muted});block.Inlines.Add(new Run(field.Value));return block;
        }
        private static Button ActionButton(string text,Style style)
        {
            var button=new Button {Content=text,Style=style,FontSize=13,Foreground=Ink,Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(201,208,219)),
                Padding=new Thickness(8,5,8,5),Margin=new Thickness(0,0,5,5),HorizontalAlignment=HorizontalAlignment.Left,ToolTip=text};
            System.Windows.Automation.AutomationProperties.SetName(button,text);return button;
        }
        private static TextBlock Text(string text,double size,Brush brush,Thickness? margin=null)
        {return new TextBlock {Text=text,FontSize=size,Foreground=brush,TextWrapping=TextWrapping.Wrap,Margin=margin ?? new Thickness(0)};}
        internal static void AddProse(Panel parent,string text)
        {
            if(string.IsNullOrWhiteSpace(text)) return;
            foreach(string paragraph in ReadableParagraphs(text))
            {
                var lines=paragraph.Split(new[] {"\r\n","\n"},StringSplitOptions.None);
                var group=new StackPanel {Margin=new Thickness(0,8,0,0)};
                foreach(string line in lines)
                {
                    var match=Regex.Match(line,@"^\s*(?:([-*•])\s+|(\d+[.)])\s+)(.*)$");
                    var block=Text("",MessageSize,Ink,new Thickness(0,0,0,3));
                    string content=match.Success?match.Groups[3].Value:line;
                    InlineCollection inlines=block.Inlines;
                    foreach(string part in Regex.Split(content,@"(\*\*[^*]+\*\*)"))
                        inlines.Add(part.StartsWith("**",StringComparison.Ordinal) && part.EndsWith("**",StringComparison.Ordinal) && part.Length>4?
                            (Inline)new Bold(new Run(part.Substring(2,part.Length-4))):new Run(part));
                    if(match.Success)
                    {
                        var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(25)});row.ColumnDefinitions.Add(new ColumnDefinition());
                        row.Children.Add(Text(match.Groups[2].Success?match.Groups[2].Value:"•",MessageSize,Ink));Grid.SetColumn(block,1);row.Children.Add(block);group.Children.Add(row);
                    }
                    else group.Children.Add(block);
                }
                parent.Children.Add(group);
            }
        }
        private static IEnumerable<string> ReadableParagraphs(string text)
        {
            foreach(string paragraph in Regex.Split(text.Trim(),@"\r?\n\s*\r?\n"))
            {
                if(paragraph.Length<=300 || Regex.IsMatch(paragraph,@"(?m)^\s*(?:[-*•]|\d+[.)])\s")) {yield return paragraph;continue;}
                var sentences=Regex.Split(paragraph,@"(?<=[.!?])\s+(?=[A-Z])");
                for(int i=0;i<sentences.Length;i+=2) yield return string.Join(" ",sentences.Skip(i).Take(2));
            }
        }
    }
}
