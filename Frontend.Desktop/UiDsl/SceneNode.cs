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
        Button,
        Input,
        Checkbox,
        RadioButton,
        Slider,
        DropDown
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
        public float BorderRadius { get; set; }
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

        // Interactive properties
        public string Value { get; set; } = string.Empty;
        public string Placeholder { get; set; } = string.Empty;
        public bool IsChecked { get; set; } = false;
        public bool IsSelected { get; set; } = false;
        public string GroupName { get; set; } = string.Empty;
        public float MinValue { get; set; } = 0;
        public float MaxValue { get; set; } = 100;
        public float CurrentValue { get; set; } = 0;

        // Event handlers (name of message to send)
        public string OnClick { get; set; } = string.Empty;
        public string OnHover { get; set; } = string.Empty;
        public string OnFocus { get; set; } = string.Empty;
        public string OnChange { get; set; } = string.Empty;
        public string OnBlur { get; set; } = string.Empty;

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
                BorderRadius = this.BorderRadius,
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
                OnFocus = this.OnFocus,
                OnChange = this.OnChange,
                OnBlur = this.OnBlur,
                Value = this.Value,
                Placeholder = this.Placeholder,
                IsChecked = this.IsChecked,
                IsSelected = this.IsSelected,
                GroupName = this.GroupName,
                MinValue = this.MinValue,
                MaxValue = this.MaxValue,
                CurrentValue = this.CurrentValue
            };
        }

        public SceneNode? HitTest(float x, float y)
        {
            // If node is not visible, it can't be hit
            if (!Visible)
                return null;
            
            // Check if point is within this node's bounds
            bool isInside = x >= X && x <= X + Width && 
                            y >= Y && y <= Y + Height;
            
            if (!isInside)
                return null;
            
            // Check children first (top-most node gets priority)
            if (Children != null)
            {
                // Iterate in reverse order to check topmost nodes first
                for (int i = Children.Count - 1; i >= 0; i--)
                {
                    // Adjust the hit test coordinates to be relative to the child's coordinate system
                    var childHit = Children[i].HitTest(x - X, y - Y);
                    if (childHit != null)
                        return childHit;
                }
            }
            
            // If no children were hit, return this node if it's interactive
            if (!string.IsNullOrEmpty(OnClick) || 
                Type == NodeType.Button || 
                Type == NodeType.Checkbox ||
                Type == NodeType.Radio)
                return this;
            
            return null;
        }

        public List<SceneNode> FindAllNodesOfType(NodeType type)
        {
            List<SceneNode> results = new List<SceneNode>();
            
            // Add this node if it matches
            if (Type == type)
            {
                results.Add(this);
            }
            
            // Search children
            if (Children != null)
            {
                foreach (var child in Children)
                {
                    results.AddRange(child.FindAllNodesOfType(type));
                }
            }
            
            return results;
        }
    }
} 