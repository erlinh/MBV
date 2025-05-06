using SkiaSharp;
using System;
using System.Collections.Generic;

namespace Frontend.Desktop.UiDsl
{
    public enum NodeType
    {
        Container,
        Text,
        Rectangle,
        Circle,
        Image,
        Button
    }

    public class SceneNode
    {
        // Basic properties
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public NodeType Type { get; set; } = NodeType.Container;
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        // Visual properties
        public SKColor? BackgroundColor { get; set; }
        public SKColor? BorderColor { get; set; }
        public float BorderWidth { get; set; }
        public SKColor? FillColor { get; set; }
        public SKColor? TextColor { get; set; }
        public float FontSize { get; set; } = 16;
        public string Text { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;

        // Layout properties
        public bool Visible { get; set; } = true;
        public string Align { get; set; } = "start"; // start, center, end
        public string JustifyContent { get; set; } = "start"; // start, center, end, space-between
        public float Margin { get; set; }
        public float Padding { get; set; }

        // Event handlers (name of message to send)
        public string OnClick { get; set; } = string.Empty;
        public string OnHover { get; set; } = string.Empty;
        public string OnFocus { get; set; } = string.Empty;

        // Node hierarchy
        public List<SceneNode> Children { get; set; } = new List<SceneNode>();
        public SceneNode? Parent { get; set; }

        // Add a child node
        public void AddChild(SceneNode child)
        {
            child.Parent = this;
            Children.Add(child);
        }

        // Clone this node (shallow copy, doesn't include children)
        public SceneNode Clone()
        {
            return new SceneNode
            {
                Type = this.Type,
                X = this.X,
                Y = this.Y,
                Width = this.Width,
                Height = this.Height,
                BackgroundColor = this.BackgroundColor,
                BorderColor = this.BorderColor,
                BorderWidth = this.BorderWidth,
                FillColor = this.FillColor,
                TextColor = this.TextColor,
                FontSize = this.FontSize,
                Text = this.Text,
                ImagePath = this.ImagePath,
                Visible = this.Visible,
                Align = this.Align,
                JustifyContent = this.JustifyContent,
                Margin = this.Margin,
                Padding = this.Padding,
                OnClick = this.OnClick,
                OnHover = this.OnHover,
                OnFocus = this.OnFocus
            };
        }
    }
} 