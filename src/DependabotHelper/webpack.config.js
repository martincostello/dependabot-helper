const path = require('path');
const webpack = require('webpack');
const CssMinimizerPlugin = require("css-minimizer-webpack-plugin");
const MiniCssExtractPlugin = require("mini-css-extract-plugin");
const RemoveEmptyScriptsPlugin = require("webpack-remove-empty-scripts");

module.exports = {
    devtool: 'source-map',
    entry: {
        css: path.resolve(__dirname, './styles/main.css'),
        js: path.resolve(__dirname, './scripts/main.ts'),
    },
    mode: 'production',
    module: {
        rules: [
            {
                test: /.css$/,
                use: [
                  MiniCssExtractPlugin.loader,
                  { loader: "css-loader", options: { sourceMap: true } },
                ],
            },
            {
                test: /\.ts$/,
                use: 'ts-loader',
                exclude: /node_modules/,
            },
        ],
    },
    optimization: {
        minimize: true,
        minimizer: [
            `...`,
            new CssMinimizerPlugin(),
        ],
    },
    output: {
        filename: '[name]/main.js',
        path: path.resolve(__dirname, 'wwwroot', 'static'),
    },
    performance: {
        hints: false,
        maxEntrypointSize: 512000,
        maxAssetSize: 512000
    },
    plugins: [
        new MiniCssExtractPlugin({
            filename: '[name]/main.css'
        }),
        new RemoveEmptyScriptsPlugin(),
        new webpack.ContextReplacementPlugin(/moment[/\\]locale$/, /en-gb/),
    ],
    resolve: {
        extensions: ['.css', '.ts', '.js'],
    },
};
