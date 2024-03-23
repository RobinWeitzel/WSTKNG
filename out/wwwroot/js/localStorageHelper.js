class Storage {
    constructor() {
        this._idKey = 'ongoingIds';

        if(localStorage.getItem(this._idKey) === null || localStorage.getItem(this._idKey) === '') {
          this.setIds([]);
        }
    }
    
    getIds() {
      return JSON.parse(localStorage.getItem(this._idKey));
    }

    addId(id) {
      const ids = this.getIds();
      ids.push(id);
      this.setIds(ids);
    }

    removeId(id) {
      const ids = this.getIds();
      const index = ids.indexOf(id);
      if (index !== -1) {
        ids.splice(index, 1);
      }
      this.setIds(ids);
    }

    setIds(ids) {
      localStorage.setItem(this._idKey, JSON.stringify(ids));
    }

    IdsToString() {
      return JSON.parse(localStorage.getItem(this._idKey));
    }
}