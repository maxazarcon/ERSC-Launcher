using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace ERSC.Launcher;

public sealed partial class MainWindow
{
    private static readonly Lazy<Style> CompanionSliderStyle = new(() => (Style)XamlReader.Parse("""
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Slider">
          <Setter Property="MinHeight" Value="48"/>
          <Setter Property="IsMoveToPointEnabled" Value="True"/>
          <Setter Property="Template"><Setter.Value>
            <ControlTemplate TargetType="Slider">
              <Grid Background="Transparent">
                <Track x:Name="PART_Track" VerticalAlignment="Center">
                  <Track.DecreaseRepeatButton><RepeatButton Command="Slider.DecreaseLarge"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Height="4" Background="#C4AD77" VerticalAlignment="Center"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
                  <Track.Thumb><Thumb Width="16" Height="48"><Thumb.Template><ControlTemplate TargetType="Thumb"><Ellipse Width="16" Height="16" Fill="#C4AD77" VerticalAlignment="Center"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                  <Track.IncreaseRepeatButton><RepeatButton Command="Slider.IncreaseLarge"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Height="4" Background="#343B30" VerticalAlignment="Center"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
                </Track>
              </Grid>
              <ControlTemplate.Triggers><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.4"/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate>
          </Setter.Value></Setter>
        </Style>
        """));

    private static readonly Lazy<Style> CompanionScrollBarStyle = new(() => (Style)XamlReader.Parse("""
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ScrollBar">
          <Setter Property="Width" Value="10"/>
          <Setter Property="Template"><Setter.Value>
            <ControlTemplate TargetType="ScrollBar">
              <Grid Background="#101411">
                <Track x:Name="PART_Track" IsDirectionReversed="True" Orientation="Vertical">
                  <Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0" Focusable="False"/></Track.DecreaseRepeatButton>
                  <Track.Thumb><Thumb MinHeight="24" Margin="2,0"><Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="#414B36" CornerRadius="3"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                  <Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0" Focusable="False"/></Track.IncreaseRepeatButton>
                </Track>
              </Grid>
            </ControlTemplate>
          </Setter.Value></Setter>
        </Style>
        """));
}
