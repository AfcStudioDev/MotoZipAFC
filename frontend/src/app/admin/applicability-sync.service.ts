import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { AdminService } from '../core/admin.service';
import { StagedModelLink, StagedSeriesLink } from './admin.types';

/**
 * Сохранение применимости парт-номера. Вынесено из компонента: это не отрисовка, а работа
 * с API — причём в несколько запросов, потому что недостающие марки, модели и серии
 * заводятся по названию прямо в процессе.
 *
 * Вызывается после сохранения самого парт-номера: до этого у новой каталожной позиции
 * ещё нет id, к которому привязывать связи.
 */
@Injectable({ providedIn: 'root' })
export class ApplicabilitySyncService {
  private readonly admin = inject(AdminService);

  /**
   * Приводит связи парт-номера к желаемому составу: недостающие марка/модель/серия
   * создаются по названию, новые связи добавляются, убранные из списка — удаляются.
   * Бросает исключение — вызывающий решает, как показать ошибку.
   */
  async sync(
    partNumId: number,
    models: StagedModelLink[],
    series: StagedSeriesLink[],
    originalModelLinkIds: number[],
    originalSeriesLinkIds: number[],
    references: Record<string, any[]>
  ): Promise<void> {
    // --- Модели ---
    const keptModelLinkIds = new Set(models.filter(m => m.applicabilityId).map(m => m.applicabilityId));
    for (const linkId of originalModelLinkIds.filter(id => !keptModelLinkIds.has(id))) {
      await firstValueFrom(this.admin.delete('applicability', linkId));
    }

    for (const m of models) {
      if (m.applicabilityId) continue; // связь уже существует

      let markId = m.markId;
      if (!markId && m.markName.trim()) {
        const existingMark = (references['marks'] ?? [])
          .find((x: any) => String(x.mark).toLowerCase() === m.markName.trim().toLowerCase());
        markId = existingMark
          ? existingMark.id
          : (await firstValueFrom(this.admin.add('marks', { mark: m.markName.trim() })) as any).id;
      }

      let modelId = m.modelId;
      if (!modelId) {
        const existingModel = (references['models'] ?? [])
          .find((x: any) => String(x.model).toLowerCase() === m.modelName.trim().toLowerCase()
            && (markId == null || x.markId === markId));
        modelId = existingModel
          ? existingModel.id
          : (await firstValueFrom(this.admin.add('models', { markId: markId ?? null, model: m.modelName.trim() })) as any).id;
      }

      await firstValueFrom(this.admin.add('applicability', { partNumId, modelId }));
    }

    // --- Серии ---
    const keptSeriesLinkIds = new Set(series.filter(s => s.applicabilityId).map(s => s.applicabilityId));
    for (const linkId of originalSeriesLinkIds.filter(id => !keptSeriesLinkIds.has(id))) {
      await firstValueFrom(this.admin.delete('series-applicability', linkId));
    }

    for (const s of series) {
      if (s.applicabilityId) continue;

      let seriesId = s.seriesId;
      if (!seriesId) {
        const existingSeries = (references['series'] ?? [])
          .find((x: any) => String(x.seriesName).toLowerCase() === s.seriesName.trim().toLowerCase());
        seriesId = existingSeries
          ? existingSeries.id
          : (await firstValueFrom(this.admin.add('series', { seriesName: s.seriesName.trim() })) as any).id;
      }

      await firstValueFrom(this.admin.add('series-applicability', { partNumId, seriesId }));
    }
  }

  /** Существующие связи редактируемого парт-номера — для заполнения формы. */
  loadForEdit(partNumId: number, references: Record<string, any[]>): {
    models: StagedModelLink[];
    series: StagedSeriesLink[];
  } {
    const models = (references['applicability'] ?? [])
      .filter((a: any) => a.partNumId === partNumId)
      .map((a: any): StagedModelLink => ({
        applicabilityId: a.id,
        modelId: a.modelId,
        markName: a.mark ?? '',
        modelName: a.model
      }));

    const series = (references['series-applicability'] ?? [])
      .filter((a: any) => a.partNumId === partNumId)
      .map((a: any): StagedSeriesLink => ({
        applicabilityId: a.id,
        seriesId: a.seriesId,
        seriesName: a.series
      }));

    return { models, series };
  }
}
