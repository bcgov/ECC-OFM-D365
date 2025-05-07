/**
 * ApplicationScoreGaugeControl
 * A PCF control to display an application score as a gauge chart using Fluent UI.
 */

import { IInputs, IOutputs } from "./generated/ManifestTypes";
import * as React from "react";
import {GaugeChartComponent } from './gauge/GaugeChart';
import { createRoot } from 'react-dom/client';

// Initialize Fluent UI icons


export class ApplicationScoreGaugeControl implements ComponentFramework.StandardControl<IInputs, IOutputs> {
    private _container: HTMLDivElement;
    private _context: ComponentFramework.Context<IInputs>;
    private _score: number | null;
    private _maxScore: number;
    private _lowSegmentPercent: number;
    private _mediumSegmentPercent: number;
    private _highSegmentPercent: number;
    private _lowSegmentLabel: string;
    private _mediumSegmentLabel: string;
    private _highSegmentLabel: string;

    
    public init(
        context: ComponentFramework.Context<IInputs>,
        notifyOutputChanged: () => void,
        state: ComponentFramework.Dictionary,
        container: HTMLDivElement
    ): void {
        this._context = context;
        this._container = container;
        this._score = context.parameters.score.raw;
        this._maxScore = context.parameters.maxScore.raw ?? 100;
        this._lowSegmentPercent = context.parameters.lowSegmentPercent.raw ?? 30;
        this._mediumSegmentPercent = context.parameters.mediumSegmentPercent.raw ?? 40;
        this._highSegmentPercent = context.parameters.highSegmentPercent.raw ?? 30;
        this._lowSegmentLabel = context.parameters.lowSegmentLabel.raw ?? "Low";
        this._mediumSegmentLabel = context.parameters.mediumSegmentLabel.raw ?? "Medium";
        this._highSegmentLabel = context.parameters.highSegmentLabel.raw ?? "High";
        this.renderControl();
    }

    public updateView(context: ComponentFramework.Context<IInputs>): void {
        this._score = context.parameters.score.raw;
        this._maxScore = context.parameters.maxScore.raw ?? 100;
        this._lowSegmentPercent = context.parameters.lowSegmentPercent.raw ?? 30;
        this._mediumSegmentPercent = context.parameters.mediumSegmentPercent.raw ?? 40;
        this._highSegmentPercent = context.parameters.highSegmentPercent.raw ?? 30;
        this._lowSegmentLabel = context.parameters.lowSegmentLabel.raw ?? "Low";
        this._mediumSegmentLabel = context.parameters.mediumSegmentLabel.raw ?? "Medium";
        this._highSegmentLabel = context.parameters.highSegmentLabel.raw ?? "High";
        this.renderControl();
    }

    private renderControl(): void {
        const element = React.createElement(GaugeChartComponent, {
            score: this._score,
            maxScore: this._maxScore,
            lowSegmentPercent: this._lowSegmentPercent,
            mediumSegmentPercent: this._mediumSegmentPercent,
            highSegmentPercent: this._highSegmentPercent,
            lowSegmentLabel: this._lowSegmentLabel,
            mediumSegmentLabel: this._mediumSegmentLabel,
            highSegmentLabel: this._highSegmentLabel
        });
        const root = createRoot(this._container);
        root.render(element);

        
    }

    public getOutputs(): IOutputs {
        return {};
    }

    public destroy(): void {
        const root = createRoot(this._container);
        root.unmount();

    }
}
