// Expo's preset handles TypeScript, JSX and the Flow-typed React Native sources in one
// pass, so the same configuration serves Metro on a device and Babel under Jest.
// The legacy decorators plugin is required by WatermelonDB's model decorators (@field,
// @date, …), which predate the current decorators proposal.
module.exports = function (api) {
  api.cache(true);

  return {
    presets: ['babel-preset-expo'],
    plugins: [
      ['@babel/plugin-proposal-decorators', { legacy: true }],
      // WatermelonDB 0.28 + TypeScript 6 require `declare` fields on decorated model
      // properties (the decorator provides the runtime getter/setter, so the field must
      // not be initialised as a class field). babel-preset-flow-strip-types strips TS,
      // so we tell it to allow declare class fields.
      ['@babel/plugin-transform-flow-strip-types', { allowDeclareFields: true }],
    ],
  };
};
