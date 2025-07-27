import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class StorageService {
  
  constructor() {}
 
  async getItem<T>(key: string): Promise<T | null> {
    try {
      const item = localStorage.getItem(key);
      if (!item) return null;
      
      return JSON.parse(item) as T;
    } catch (error) {
      console.warn(`Erro ao recuperar item '${key}' do storage:`, error);
      return null;
    }
  }

  getItemSync<T>(key: string): T | null {
    try {
      const item = localStorage.getItem(key);
      if (!item) return null;
      
      return JSON.parse(item) as T;
    } catch (error) {
      console.warn(`Erro ao recuperar item '${key}' do storage:`, error);
      return null;
    }
  }
  
  async setItem<T>(key: string, value: T): Promise<void> {
    try {
      const serialized = JSON.stringify(value);
      localStorage.setItem(key, serialized);
    } catch (error) {
      console.error(`Erro ao salvar item '${key}' no storage:`, error);
      throw error;
    }
  }

  setItemSync<T>(key: string, value: T): void {
    try {
      const serialized = JSON.stringify(value);
      localStorage.setItem(key, serialized);
    } catch (error) {
      console.error(`Erro ao salvar item '${key}' no storage:`, error);
      throw error;
    }
  }

  async removeItem(key: string): Promise<void> {
    try {
      localStorage.removeItem(key);
    } catch (error) {
      console.error(`Erro ao remover item '${key}' do storage:`, error);
    }
  }

  removeItemSync(key: string): void {
    try {
      localStorage.removeItem(key);
    } catch (error) {
      console.error(`Erro ao remover item '${key}' do storage:`, error);
    }
  }
  
  async clear(): Promise<void> {
    try {
      localStorage.clear();
    } catch (error) {
      console.error('Erro ao limpar storage:', error);
    }
  }

  clearSync(): void {
    try {
      localStorage.clear();
    } catch (error) {
      console.error('Erro ao limpar storage:', error);
    }
  }

  async getAllKeys(): Promise<string[]> {
    try {
      return Object.keys(localStorage);
    } catch (error) {
      console.error('Erro ao recuperar chaves do storage:', error);
      return [];
    }
  }

  getAllKeysSync(): string[] {
    try {
      return Object.keys(localStorage);
    } catch (error) {
      console.error('Erro ao recuperar chaves do storage:', error);
      return [];
    }
  }

  async getStorageSize(): Promise<number> {
    try {
      let totalSize = 0;
      for (const key in localStorage) {
        if (localStorage.hasOwnProperty(key)) {
          totalSize += localStorage.getItem(key)?.length || 0;
          totalSize += key.length;
        }
      }
      return totalSize;
    } catch (error) {
      console.error('Erro ao calcular tamanho do storage:', error);
      return 0;
    }
  }
  
  async getAppData<T>(module: string, key: string): Promise<T | null> {
    const fullKey = `nba_app_${module}_${key}`;
    return this.getItem<T>(fullKey);
  }

  async setAppData<T>(module: string, key: string, value: T): Promise<void> {
    const fullKey = `nba_app_${module}_${key}`;
    return this.setItem(fullKey, value);
  }

  async removeAppData(module: string, key: string): Promise<void> {
    const fullKey = `nba_app_${module}_${key}`;
    return this.removeItem(fullKey);
  }

  async clearAppData(module?: string): Promise<void> {
    try {
      const prefix = module ? `nba_app_${module}_` : 'nba_app_';
      const keys = await this.getAllKeys();
      
      const keysToRemove = keys.filter(key => key.startsWith(prefix));
      
      for (const key of keysToRemove) {
        await this.removeItem(key);
      }
      
      console.log(`🗑️ Limpeza concluída: ${keysToRemove.length} itens removidos`);
    } catch (error) {
      console.error('Erro ao limpar dados do app:', error);
    }
  }
}