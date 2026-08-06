// Expo's preset handles TypeScript, JSX and the Flow-typed React Native sources in one
// pass, so the same configuration serves Metro on a device and Babel under Jest.
//
// Plugin order is not negotiable. The two pitfalls WatermelonDB trips on:
//
//   1. `@babel/plugin-transform-typescript` MUST run before any class-feature plugin
//      (decorators, class-properties, private-methods). Otherwise the `declare sex: string`
//      pattern in the model classes is still on the AST when the decorator walks it, and
//      Babel emits "TypeScript 'declare' fields must first be transformed".
//
//   2. `@babel/plugin-proposal-decorators` MUST run before
//      `@babel/plugin-transform-class-properties` because WatermelonDB's decorators own
//      the runtime field — the property must NOT exist on the instance before the
//      decorator wraps it, otherwise "Decorating class property failed" appears the first
//      time the model class is instantiated at runtime.
module.exports = function (api) {
  api.cache(true);

  return {
    presets: ['babel-preset-expo'],
    plugins: [
      // Run TypeScript first so `declare` is gone before class-feature plugins touch
      // the AST. babel-preset-expo already includes preset-typescript, but the project's
      // own plugins run after the preset, so by the time decorators inspect the class the
      // declare fields need to be absent. Adding the plugin explicitly pins the order.
      // `allowDeclareFields: true` lets the WatermelonDB models keep the `declare` fields
      // around — the decorator then owns the runtime getter/setter for that column.
      ['@babel/plugin-transform-typescript', { allowDeclareFields: true, isTSX: true }],
      ['@babel/plugin-proposal-decorators', { legacy: true }],
      ['@babel/plugin-transform-class-properties', { loose: true }],
      // Private methods (`#foo() {}`) appear in node_modules/react-native. Without this
      // plugin the bundler trips on the first class with a private member.
      ['@babel/plugin-transform-private-methods', { loose: true }],
    ],
  };
};
