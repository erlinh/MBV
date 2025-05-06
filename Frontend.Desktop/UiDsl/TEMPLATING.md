# MBV UI Templating System

The MBV UI templating system provides powerful features for creating reusable components with flexible layouts and data binding.

## Basic Component Usage

```xml
<include component="ComponentName" prop1="value1" prop2="value2">
  <!-- Children content goes here -->
</include>
```

## Property Binding

Properties are passed to components using attributes and accessed in templates using `{propertyName}` syntax:

```xml
<!-- Component usage -->
<include component="Button" text="Click me" onClick="SomeAction" />

<!-- Inside Button.skx -->
<box onClick="{onClick}">
  <text text="{text}" />
</box>
```

## Default Values

Properties can have default values using the `||` operator:

```xml
<text text="{title || 'Default Title'}" />
```

## Conditional Expressions

You can use conditional expressions for properties:

```xml
<box fill="{isActive ? '#DBEAFE' : '#E0F2FE'}" />
```

## Math Expressions

Simple math expressions are supported:

```xml
<view width="{containerWidth - 40}" />
```

## Slots and Content Projection

Components can define slots for content projection:

### Default Slot

```xml
<!-- Component usage -->
<include component="Card">
  <text>This content goes in the default slot</text>
</include>

<!-- Inside Card.skx -->
<view>
  <slot />  <!-- Default slot -->
</view>
```

### Named Slots

```xml
<!-- Component usage -->
<include component="Card">
  <template slot="header">
    <text>Custom Header</text>
  </template>
  <text>Main content</text>
</include>

<!-- Inside Card.skx -->
<view>
  <slot name="header">
    <!-- Default header content if none provided -->
    <text>Default Header</text>
  </slot>
  <slot />  <!-- Default slot for main content -->
</view>
```

## Component Composition

Components can be composed together to create complex UIs:

```xml
<include component="Form">
  <include component="FormField" label="Name" />
  <include component="FormField" label="Email" />
  <include component="Button" text="Submit" />
</include>
```

## Component Inheritance

There is no formal inheritance, but you can compose components to achieve similar results:

```xml
<include component="SpecialButton" text="Click Me" special="true">
  <include component="Button" text="{text}" isSpecial="{special}" />
</include>
```

## Advanced Features

### Dynamic Components (Coming soon)

```xml
<view>
  {#each items as item}
    <include component="ListItem" text="{item.text}" />
  {/each}
</view>
```

### Two-way Binding (Coming soon)

```xml
<include component="Input" value="{username}" onChange="SetUsername" />
```

## Creating New Components

1. Create a new .skx file in the Components directory
2. Use placeholder syntax `{propertyName}` for dynamic values
3. Use slots for content projection with `<slot />` or `<slot name="slotName" />`
4. Test your component by including it in a UI file 