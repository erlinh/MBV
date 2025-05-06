# MBV UI Components

This directory contains reusable UI components for the MBV application. These components can be included in any .skx file using the `<include>` tag.

## Usage

```xml
<include component="ComponentName" prop1="value1" prop2="value2">
  <!-- Children content goes here (if applicable) -->
</include>
```

## Available Components

### Button

A reusable button component with customizable properties.

```xml
<include component="Button" 
  id="my-button"
  x="20" 
  y="140" 
  width="200" 
  height="50" 
  fillColor="#3B82F6" 
  borderColor="#1E40AF" 
  borderWidth="2" 
  onClick="SomeAction:param"
  textX="40"
  textY="15"
  text="Button Text"
  fontSize="18"
  textColor="White" />
```

### Header

A standard application header with title and user display.

```xml
<include component="Header" 
  width="800" 
  title="Application Title" 
  userDisplay="User: Guest" />
```

### NavItem

A navigation menu item for the sidebar.

```xml
<include component="NavItem" 
  id="nav-item-id" 
  x="10" 
  y="50" 
  width="180" 
  text="Menu Item" 
  isActive="true" 
  onClick="NavigateTo:destination" />
```

### Sidebar

A sidebar container for navigation items.

```xml
<include component="Sidebar" topY="60" height="540">
  <!-- NavItem components go here -->
  <include component="NavItem" ... />
  <include component="NavItem" ... />
</include>
```

### ContentArea

A content area for the main application content.

```xml
<include component="ContentArea" x="200" y="60" width="600" height="540">
  <!-- Content goes here -->
  <text x="20" y="30" text="Title" fontSize="32" textColor="#334155" />
</include>
```

## Component Development

To create a new component:

1. Create a new .skx file in the Components directory
2. Use placeholders like `{propertyName}` for dynamic values
3. For components that contain children, use `{content}` or another placeholder
4. Test your component in main.skx 